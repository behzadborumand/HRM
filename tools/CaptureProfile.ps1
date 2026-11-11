param(
    [Parameter(Mandatory=$true)][string]$BaseUrl,
    [Parameter(Mandatory=$true)][string]$Username,
    [Parameter(Mandatory=$true)][string]$Password,
    [Parameter(Mandatory=$true)][int]$EmployeeId,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [int]$DebugPort = 52329
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
    throw 'Browser did not reach the expected UI state.'
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
    if (!(Evaluate "document.querySelectorAll('#tab-leave [data-profile-request-id]').length===6 && document.querySelectorAll('#tab-training [data-profile-assignment-id]').length===6")) { throw 'Profile activity pagination did not retain both page selections.' }
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
