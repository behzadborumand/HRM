param(
    [Parameter(Mandatory=$true)][string]$BaseUrl,
    [Parameter(Mandatory=$true)][string]$Username,
    [Parameter(Mandatory=$true)][string]$Password,
    [Parameter(Mandatory=$true)][int]$EmployeeId,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [int]$DebugPort = 52329,
    [string]$SelfUsername,
    [string]$SelfPassword
)
$ErrorActionPreference='Stop'
if ([Uri]$BaseUrl -and ([Uri]$BaseUrl).Host -notin @('localhost','127.0.0.1')) { throw 'Visual smoke is restricted to localhost.' }
$browserExe=@('C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe','C:\Program Files\Google\Chrome\Application\chrome.exe') | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (!$browserExe) { throw 'A local Chromium browser is required.' }
if (Get-NetTCPConnection -LocalPort $DebugPort -State Listen -ErrorAction SilentlyContinue) { throw 'Visual debug port is already occupied.' }
$profile=Join-Path $env:TEMP ('hrm-profile-browser-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$browserProcess=$null; $socket=$null; $script:messageId=0; $script:browserErrors=@()

function Send-Cdp([string]$method, [hashtable]$parameters=@{}) {
    $script:messageId++
    $message=@{id=$script:messageId;method=$method;params=$parameters} | ConvertTo-Json -Depth 20 -Compress
    $bytes=[Text.Encoding]::UTF8.GetBytes($message)
    $segment=[ArraySegment[byte]]::new($bytes)
    $socket.SendAsync($segment,[Net.WebSockets.WebSocketMessageType]::Text,$true,[Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
    return $script:messageId
}
function Invoke-Cdp([string]$method, [hashtable]$parameters=@{}) {
    $requestId=Send-Cdp $method $parameters
    $cancel=[Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(25))
    try {
        while($true) {
            $stream=[IO.MemoryStream]::new()
            try {
                do {
                    $buffer=New-Object byte[] 65536
                    $received=$socket.ReceiveAsync([ArraySegment[byte]]::new($buffer),$cancel.Token).GetAwaiter().GetResult()
                    if($received.MessageType -eq [Net.WebSockets.WebSocketMessageType]::Close){throw 'Browser connection closed.'}
                    $stream.Write($buffer,0,$received.Count)
                } while(!$received.EndOfMessage)
                $response=[Text.Encoding]::UTF8.GetString($stream.ToArray()) | ConvertFrom-Json
            } finally { $stream.Dispose() }
            if($response.method -eq 'Runtime.exceptionThrown') { $script:browserErrors += $response.params.exceptionDetails.text }
            if($response.id -eq $requestId) {
                if($response.error){throw ("CDP " + $method + ": " + $response.error.message)}
                return $response.result
            }
        }
    } finally { $cancel.Dispose() }
}
function Evaluate([string]$expression) {
    $result=Invoke-Cdp 'Runtime.evaluate' @{expression=$expression;awaitPromise=$true;returnByValue=$true}
    if($result.exceptionDetails){throw ("Browser evaluation failed: "+$result.exceptionDetails.text)}
    return $result.result.value
}
function Wait-Expression([string]$expression) {
    for($attempt=0;$attempt -lt 50;$attempt++){
        try { if(Evaluate $expression){return} } catch { }
        Start-Sleep -Milliseconds 150
    }
    $state=Evaluate "JSON.stringify({path:location.pathname,hash:location.hash,title:document.title,ready:document.readyState})"
    Capture 'wait-failure.png'
    throw ("Browser wait failed: "+$expression+"; actual="+$state)
}
function Capture([string]$filename) {
    Evaluate 'document.fonts.ready.then(() => true)' | Out-Null
    $shot=Invoke-Cdp 'Page.captureScreenshot' @{format='png';captureBeyondViewport=$false}
    [IO.File]::WriteAllBytes((Join-Path $OutputDirectory $filename),[Convert]::FromBase64String($shot.data))
}
try {
    $arguments=@('--headless=new',('--remote-debugging-port='+$DebugPort),('--user-data-dir="'+$profile+'"'),'--no-first-run','--no-default-browser-check','--ignore-certificate-errors','--disable-background-networking','about:blank')
    $browserProcess=Start-Process -FilePath $browserExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $page=$null
    for($attempt=0;$attempt -lt 40;$attempt++){
        try { $targets=Invoke-RestMethod "http://127.0.0.1:$DebugPort/json/list"; $page=$targets | Where-Object type -eq 'page' | Select-Object -First 1; if($page){break} } catch {}
        Start-Sleep -Milliseconds 200
    }
    if(!$page){throw 'Headless browser did not start.'}
    $socket=[Net.WebSockets.ClientWebSocket]::new()
    $socket.ConnectAsync([Uri]$page.webSocketDebuggerUrl,[Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
    Invoke-Cdp 'Page.enable' | Out-Null
    Invoke-Cdp 'Runtime.enable' | Out-Null
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=1280;height=850;deviceScaleFactor=1;mobile=$false} | Out-Null
    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/Login')} | Out-Null
    Wait-Expression "!!document.querySelector('[name=Username]')"
    $userJson=$Username | ConvertTo-Json -Compress
    $passwordJson=$Password | ConvertTo-Json -Compress
    Evaluate ("document.querySelector('[name=Username]').value="+$userJson+";document.querySelector('[name=Password]').value="+$passwordJson+";document.querySelector('form[method=post]').requestSubmit();true") | Out-Null
    Wait-Expression "location.pathname==='/' && !!document.querySelector('.home-workspace')"
    Wait-Expression "document.readyState==='complete' && document.querySelectorAll('[data-celebration]').length>=2"
    if(!(Evaluate "!!document.querySelector('[data-celebration=Birthday]') && !!document.querySelector('[data-celebration=Anniversary]')")){throw 'Home browser celebration kinds missing.'}
    if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Home desktop overflow.'}
    if(!(Evaluate "!!document.querySelector('.home-sidebar > .home-celebrations')")){throw 'Home card is outside the legacy sidebar.'}
    Capture 'home-desktop.png'
    Evaluate "document.querySelector('.home-celebrations').scrollIntoView({block:'center',behavior:'instant'});true" | Out-Null
    Wait-Expression "(() => { const r=document.querySelector('.home-celebrations').getBoundingClientRect(); return r.top>=0 && r.bottom<=innerHeight; })()"
    Capture 'home-celebrations-desktop.png'
    Evaluate "document.querySelector('[data-celebration]').focus();true" | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{type='keyDown';key='Enter';code='Enter';windowsVirtualKeyCode=13;text=([string][char]13);unmodifiedText=([string][char]13)} | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{type='keyUp';key='Enter';code='Enter';windowsVirtualKeyCode=13} | Out-Null
    Wait-Expression "location.pathname.startsWith('/Employees/') && !!document.querySelector('.employee-content')"
    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/')} | Out-Null
    Wait-Expression "location.pathname==='/' && !!document.querySelector('.home-celebrations') && document.readyState==='complete'"
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=390;height=844;deviceScaleFactor=1;mobile=$true} | Out-Null
    if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Home mobile overflow.'}
    Evaluate "document.querySelector('.home-celebrations').scrollIntoView({block:'center',behavior:'instant'});true" | Out-Null
    Wait-Expression "(() => { const r=document.querySelector('.home-celebrations').getBoundingClientRect(); return r.top>=0 && r.bottom<=innerHeight; })()"
    Capture 'home-celebrations-mobile.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=1280;height=850;deviceScaleFactor=1;mobile=$false} | Out-Null

    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/Employees?query=DIRECTORY_UI_&status=Active')} | Out-Null
    Wait-Expression "document.readyState==='complete' && document.querySelectorAll('[data-employee-id]').length===20"
    Wait-Expression "(() => {const i=document.querySelector('[data-directory-photo]');return !!i && i.complete && i.naturalWidth>0;})()"
    if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Directory desktop page overflow.'}
    Capture 'directory-desktop.png'
    Evaluate "document.querySelector('.directory-pagination a').focus();true" | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{type='keyDown';key='Enter';code='Enter';windowsVirtualKeyCode=13;text=([string][char]13);unmodifiedText=([string][char]13)} | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{type='keyUp';key='Enter';code='Enter';windowsVirtualKeyCode=13} | Out-Null
    Wait-Expression "location.search.includes('pageNumber=2') && document.querySelectorAll('[data-employee-id]').length===4"
    Capture 'directory-page2.png'
    Evaluate "document.querySelector('#query').value='DIRECTORY_UI_000';document.querySelector('#query').focus();true" | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{type='keyDown';key='Enter';code='Enter';windowsVirtualKeyCode=13;text=([string][char]13);unmodifiedText=([string][char]13)} | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{type='keyUp';key='Enter';code='Enter';windowsVirtualKeyCode=13} | Out-Null
    Wait-Expression "location.search.includes('DIRECTORY_UI_000') && document.querySelectorAll('[data-employee-id]').length===1 && !location.search.includes('pageNumber=2')"
    Capture 'directory-filter.png'
    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/Employees?query=DIRECTORY_UI_&status=Active')} | Out-Null
    Wait-Expression "document.readyState==='complete' && document.querySelectorAll('[data-employee-id]').length===20"
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=390;height=844;deviceScaleFactor=1;mobile=$true} | Out-Null
    if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Directory mobile page overflow.'}
    if(!(Evaluate "getComputedStyle(document.querySelector('.table-responsive')).overflowX==='auto'")){throw 'Directory mobile table cannot scroll.'}
    Capture 'directory-mobile.png'
    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/Employees?query=DIRECTORY_NO_MATCH')} | Out-Null
    Wait-Expression "document.readyState==='complete' && document.querySelectorAll('[data-employee-id]').length===0 && !!document.querySelector('td[colspan]')"
    Capture 'directory-empty-mobile.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=1280;height=850;deviceScaleFactor=1;mobile=$false} | Out-Null

    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/Employees/Create')} | Out-Null
    Wait-Expression "document.readyState==='complete' && !!document.querySelector('#Form_FirstName') && document.body.classList.contains('page-profile')"
    if(!(Evaluate "getComputedStyle(document.querySelector('#employee-information .card-header')).backgroundColor==='rgb(255, 255, 255)' && !document.querySelector('.profile-hero-avatar')")){throw 'Create form has generic bars or a fictitious employee portrait.'}
    if(!(Evaluate "document.querySelector('.profile-create-hero')?.getBoundingClientRect().height>=90")){throw 'Create identity header is collapsed.'}
    Capture 'create-desktop.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=390;height=844;deviceScaleFactor=1;mobile=$true} | Out-Null
    if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Create mobile overflow.'}
    Capture 'create-mobile.png'
    Evaluate "document.querySelector('#Form_LastName').value='Preserved create input';const f=document.querySelector('#employee-information form');f.noValidate=true;f.requestSubmit();true" | Out-Null
    Wait-Expression "!!document.querySelector('.validation-summary-errors') && document.querySelector('#Form_LastName').value==='Preserved create input' && document.body.classList.contains('page-profile')"
    Capture 'create-invalid-mobile.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=1280;height=850;deviceScaleFactor=1;mobile=$false} | Out-Null


    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/Settings/Roles')} | Out-Null
    Wait-Expression "!!document.querySelector('[name=Name]') && document.readyState==='complete'"
    Capture 'roles-desktop.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=390;height=844;deviceScaleFactor=1;mobile=$true} | Out-Null
    if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Roles mobile overflow.'}
    Capture 'roles-mobile.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=1280;height=850;deviceScaleFactor=1;mobile=$false} | Out-Null
    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/Employees/'+$EmployeeId+'/Edit')} | Out-Null
    Wait-Expression "document.body.classList.contains('page-profile') && !!document.querySelector('#Form_FirstName') && document.readyState==='complete'"
    if(!(Evaluate "document.querySelector('.employee-sidebar').getBoundingClientRect().right < document.querySelector('.employee-content').getBoundingClientRect().left")){throw 'Edit desktop profile rail is missing or misplaced.'}
    if(!(Evaluate "getComputedStyle(document.querySelector('#employee-information .card-header')).backgroundColor==='rgb(255, 255, 255)'")){throw 'Edit retains generic green card bars.'}
    Capture 'edit-desktop.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=919;height=850;deviceScaleFactor=1;mobile=$false} | Out-Null
    if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Edit reference-width overflow.'}
    Capture 'edit-reference-width.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=390;height=844;deviceScaleFactor=1;mobile=$true} | Out-Null
    if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Edit mobile overflow.'}
    Capture 'edit-mobile.png'
    Evaluate "document.querySelector('.profile-tabs a[href$=job-change]').focus();true" | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{type='keyDown';key='Enter';code='Enter';windowsVirtualKeyCode=13;text=([string][char]13);unmodifiedText=([string][char]13)} | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{type='keyUp';key='Enter';code='Enter';windowsVirtualKeyCode=13} | Out-Null
    Wait-Expression "location.hash==='#job-change' && document.querySelector('#job-change').getBoundingClientRect().top<innerHeight"
    Evaluate "document.querySelector('#job-change').scrollIntoView({block:'start',behavior:'instant'});true" | Out-Null
    Capture 'edit-job-mobile.png'
    Evaluate "document.querySelector('#Form_FirstName').value='';document.querySelector('#Form_LastName').value='Preserved editor input';const f=document.querySelector('#employee-information form');f.noValidate=true;f.requestSubmit();true" | Out-Null
    Wait-Expression "!!document.querySelector('.validation-summary-errors') && !!document.querySelector('.profile-hero-avatar') && document.querySelector('#Form_LastName').value==='Preserved editor input'"
    if(!(Evaluate "document.body.classList.contains('page-profile') && document.documentElement.scrollWidth<=innerWidth+1")){throw 'Invalid Edit POST lost its profile layout.'}
    Evaluate "document.querySelector('#employee-information').scrollIntoView({block:'start',behavior:'instant'});true" | Out-Null
    Capture 'edit-invalid-mobile.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=1280;height=850;deviceScaleFactor=1;mobile=$false} | Out-Null

    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/Employees/'+$EmployeeId+'/Personal')} | Out-Null
    Wait-Expression "!!document.querySelector('[data-personal-identity]') && document.readyState==='complete'"
    if(!(Evaluate "document.body.classList.contains('page-profile') && !!document.querySelector('.profile-hero-avatar') && !!document.querySelector('.employee-sidebar')")){throw 'Personal admin editor lacks profile shell.'}
    Capture 'personal-admin-desktop.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=390;height=844;deviceScaleFactor=1;mobile=$true} | Out-Null
    if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Admin personal mobile overflow.'}
    Capture 'personal-admin-mobile.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=1280;height=850;deviceScaleFactor=1;mobile=$false} | Out-Null

    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/Employees/'+$EmployeeId)} | Out-Null
    Wait-Expression "!!document.querySelector('#profile-job-tab') && document.readyState==='complete'"
    Capture 'profile-desktop.png'
    if(!(Evaluate "document.querySelector('.employee-sidebar').getBoundingClientRect().right < document.querySelector('.employee-content').getBoundingClientRect().left")){throw 'Desktop rail is not beside the profile content.'}
    Evaluate "document.querySelector('#profile-job-tab').focus();true" | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{type='keyDown';key='ArrowRight';code='ArrowRight';windowsVirtualKeyCode=39} | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{type='keyUp';key='ArrowRight';code='ArrowRight';windowsVirtualKeyCode=39} | Out-Null
    Wait-Expression "document.querySelector('#profile-leave-tab').getAttribute('aria-selected')==='true' && document.activeElement.id==='profile-leave-tab'"
    Evaluate "document.querySelector('#profile-personal-tab').click();true" | Out-Null
    Wait-Expression "document.querySelector('#tab-personal').classList.contains('active')"
    Capture 'profile-personal.png'
    Evaluate "document.querySelector('#profile-pay-tab').click();true" | Out-Null
    Wait-Expression "document.querySelector('#tab-pay').classList.contains('active')"
    Capture 'profile-pay.png'
    Evaluate "document.querySelector('#profile-leave-tab').click();true" | Out-Null
    Wait-Expression "document.querySelector('#tab-leave').classList.contains('active')"
    Capture 'profile-leave.png'
    Evaluate "document.querySelector('#profile-training-tab').click();true" | Out-Null
    Wait-Expression "document.querySelector('#tab-training').classList.contains('active')"
    Capture 'profile-training.png'
    Evaluate "document.querySelector('[data-activity-page=training-next]').click();true" | Out-Null
    Wait-Expression "location.search.includes('trainingPage=2') && !!document.querySelector('#tab-training.active') && document.readyState==='complete'"
    Evaluate "document.querySelector('#profile-leave-tab').click();document.querySelector('[data-activity-page=leave-next]').click();true" | Out-Null
    Wait-Expression "location.search.includes('leavePage=2') && location.search.includes('trainingPage=2') && !!document.querySelector('#tab-leave.active') && document.readyState==='complete'"
    if (!(Evaluate "document.querySelectorAll('#tab-leave [data-profile-request-id]').length===8 && document.querySelectorAll('#tab-training [data-profile-assignment-id]').length===6")) { throw 'Profile activity pagination did not retain both page selections.' }
    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/Employees/'+$EmployeeId)} | Out-Null
    Wait-Expression "!!document.querySelector('#tab-job.active') && document.readyState==='complete'"
    Evaluate "document.querySelector('#profile-job-tab').click();true" | Out-Null
    Wait-Expression "document.querySelector('#tab-job').classList.contains('active')"
    Evaluate "document.activeElement.blur();true" | Out-Null
    Start-Sleep -Milliseconds 350
    if (!(Evaluate "document.querySelectorAll('.tab-pane.active').length===1")) { throw 'Rapid tab changes left multiple panels visible.' }
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=919;height=650;deviceScaleFactor=1;mobile=$false} | Out-Null
    Capture 'profile-reference-width.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=390;height=844;deviceScaleFactor=1;mobile=$true} | Out-Null
    Capture 'profile-mobile.png'
    if(!(Evaluate "document.documentElement.scrollWidth <= innerWidth+1")){throw 'Mobile profile overflows the viewport.'}
    foreach ($activityTab in @('leave','training')) {
        Evaluate ("document.querySelector('#profile-"+$activityTab+"-tab').click();true") | Out-Null
        Wait-Expression ("!!document.querySelector('#tab-"+$activityTab+".active')")
        if(!(Evaluate "document.documentElement.scrollWidth <= innerWidth+1")) { throw 'Mobile activity profile overflows the viewport.' }
        if(!(Evaluate "(() => { const tab=document.querySelector('.profile-tabs [aria-selected=true]').getBoundingClientRect(); const strip=document.querySelector('.profile-tabs').getBoundingClientRect(); return tab.left >= strip.left-1 && tab.right <= strip.right+1; })()")) { throw 'Selected mobile tab is clipped outside the tab strip.' }
        Capture ("profile-mobile-"+$activityTab+".png")
    }
    Evaluate "document.querySelector('.profile-photo-trigger').click();true" | Out-Null
    Wait-Expression "document.querySelector('#photo-modal').classList.contains('show') && getComputedStyle(document.querySelector('#photo-modal')).opacity === '1'"
    Capture 'profile-photo-dialog.png'
    if ($SelfUsername) {
        Evaluate "document.querySelector('#photo-modal [data-bs-dismiss=modal]').click();true" | Out-Null
        Wait-Expression "!document.querySelector('#photo-modal').classList.contains('show')"
        Evaluate "document.querySelector('form[action=`"/Logout`"]').requestSubmit();true" | Out-Null
        Wait-Expression "location.pathname==='/Login' && !!document.querySelector('[name=Username]')"
        $selfUserJson=$SelfUsername | ConvertTo-Json -Compress
        $selfPasswordJson=$SelfPassword | ConvertTo-Json -Compress
        Evaluate ("document.querySelector('[name=Username]').value="+$selfUserJson+";document.querySelector('[name=Password]').value="+$selfPasswordJson+";document.querySelector('form[method=post]').requestSubmit();true") | Out-Null
        Wait-Expression "location.pathname==='/' && !!document.querySelector('.home-personal-leave')"
        Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=1280;height=850;deviceScaleFactor=1;mobile=$false} | Out-Null
        Capture 'self-home.png'
        if (Evaluate "!!document.querySelector('form[role=search]')") { throw 'Owner-only browser sees directory search.' }
        Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/MyInfo')} | Out-Null
        Wait-Expression "location.pathname==='/Employees/$EmployeeId' && !!document.querySelector('#profile-job-tab') && document.readyState==='complete'"
        if (Evaluate "!!document.querySelector('.profile-neighbours,.profile-photo-trigger,a[aria-label=`"فهرست کارکنان`"]')") { throw 'Owner-only profile has inaccessible directory/write controls.' }
        Evaluate "document.querySelector('#profile-training-tab').click();true" | Out-Null
        Wait-Expression "!!document.querySelector('#tab-training.active')"
        Capture 'self-training.png'
        Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=390;height=844;deviceScaleFactor=1;mobile=$true} | Out-Null
        Wait-Expression "(() => { const tab=document.querySelector('.profile-tabs [aria-selected=true]').getBoundingClientRect(); const strip=document.querySelector('.profile-tabs').getBoundingClientRect(); return tab.left>=strip.left-1 && tab.right<=strip.right+1; })()"
        Capture 'self-profile-mobile.png'
        if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Owner profile mobile overflow.'}
        Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/My/Personal')} | Out-Null
        Wait-Expression "!!document.querySelector('[data-personal-contact]') && document.readyState==='complete'"
        if(Evaluate "!!document.querySelector('[data-personal-identity]')"){throw 'Owner sees admin identity form.'}
        if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Owner personal mobile overflow.'}
        Capture 'personal-owner-mobile.png'
        Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=1280;height=850;deviceScaleFactor=1;mobile=$false} | Out-Null
        if(!(Evaluate "document.body.classList.contains('page-profile') && !!document.querySelector('.profile-hero-avatar') && !document.querySelector('.employee-sidebar .profile-team-person')")){throw 'Owner editor shell/navigation privacy failed.'}
        Capture 'personal-owner-desktop.png'
        Evaluate "document.querySelector('#Contact_City').value='Browser personal city';document.querySelector('[data-personal-contact]').requestSubmit();true" | Out-Null
        Wait-Expression "location.pathname==='/Employees/$EmployeeId' && !!document.querySelector('#tab-personal.active') && document.body.textContent.includes('Browser personal city')"
        Capture 'personal-profile-desktop.png'
        Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=390;height=844;deviceScaleFactor=1;mobile=$true} | Out-Null
        if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Private personal profile mobile overflow.'}
        Capture 'personal-profile-mobile.png'

        Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/My/Leave')} | Out-Null
        Wait-Expression "!!document.querySelector('#own-policy') && document.readyState==='complete'"
        Evaluate "document.querySelector('#own-policy').selectedIndex=1;document.querySelector('#own-start').value='1403/04/10';document.querySelector('#own-end').value='1403/04/10';document.querySelector('#own-reason').value='Browser self-service';document.querySelector('form[action*=Request]').requestSubmit();true" | Out-Null
        Wait-Expression "!!document.querySelector('[role=status]') && document.querySelector('[role=status]').textContent.includes('ثبت شد')"
        if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Personal Leave mobile overflow.'}
        Capture 'self-leave-mobile.png'
        Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=1280;height=850;deviceScaleFactor=1;mobile=$false} | Out-Null
        Capture 'self-leave.png'
        Evaluate "Array.from(document.querySelectorAll('[data-own-request-id]')).find(row => row.textContent.includes('۱۴۰۳/۰۴/۱۰')).querySelector('form').requestSubmit();true" | Out-Null
        Wait-Expression "!!document.querySelector('[role=status]') && document.querySelector('[role=status]').textContent.includes('لغو شد')"
    }
    if($script:browserErrors.Count){throw ("JavaScript errors: "+($script:browserErrors -join ', '))}
    Write-Output ("Profile desktop/mobile, keyboard tabs and photo dialog: PASS; screenshots="+$OutputDirectory)
} finally {
    if($socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open){try{Send-Cdp 'Browser.close' | Out-Null}catch{}}
    if($socket){$socket.Dispose()}
    if($browserProcess){if(!$browserProcess.WaitForExit(4000)){Stop-Process -Id $browserProcess.Id -Force -ErrorAction SilentlyContinue}}
    if([IO.Path]::GetFullPath((Split-Path -Parent $profile)).TrimEnd('\') -eq [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') -and (Split-Path -Leaf $profile).StartsWith('hrm-profile-browser-')){
        Remove-Item -LiteralPath $profile -Recurse -Force -ErrorAction SilentlyContinue
    }
}
