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
    throw ('Browser state timeout: '+$expression+'; actual='+(Evaluate 'JSON.stringify({path:location.pathname,query:location.search,ready:document.readyState,rows:document.querySelectorAll("[data-team-employee]").length})'))
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
    Wait-Expression "location.pathname==='/' && !!document.querySelector('a[href=`"/Team`"]')"
    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/Team')} | Out-Null
    Wait-Expression "!!document.querySelector('[data-team-employee]') && document.readyState==='complete'"
    Capture 'team-desktop.png'
    Evaluate "document.querySelector('#Query').focus();true" | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{type='keyDown';key='Enter';code='Enter';windowsVirtualKeyCode=13;text=([string][char]13);unmodifiedText=([string][char]13)} | Out-Null
    Invoke-Cdp 'Input.dispatchKeyEvent' @{type='keyUp';key='Enter';code='Enter';windowsVirtualKeyCode=13} | Out-Null
    Wait-Expression "location.search.includes('Query=') && !!document.querySelector('[data-team-employee]') && document.readyState==='complete'"
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=390;height=844;deviceScaleFactor=1;mobile=$true} | Out-Null
    if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Mobile team overflow.'}
    Capture 'team-mobile.png'
    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/Team/Leave')} | Out-Null
    Wait-Expression "!!document.querySelector('[data-team-request]') && document.readyState==='complete'"
    if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Mobile team Leave overflow.'}
    Capture 'team-leave-mobile.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=1280;height=850;deviceScaleFactor=1;mobile=$false} | Out-Null
    Capture 'team-leave-desktop.png'
    Invoke-Cdp 'Page.navigate' @{url=($BaseUrl+'/Employees/'+$EmployeeId)} | Out-Null
    Wait-Expression "!!document.querySelector('#profile-job-tab') && document.readyState==='complete'"
    if(Evaluate "!!document.querySelector('#profile-pay-tab,#profile-documents-tab,#profile-training-tab,#profile-leave-tab,.profile-photo-trigger,.profile-neighbours')"){throw 'Manager browser leaked private/write controls.'}
    Capture 'team-profile-desktop.png'
    Invoke-Cdp 'Emulation.setDeviceMetricsOverride' @{width=390;height=844;deviceScaleFactor=1;mobile=$true} | Out-Null
    if(!(Evaluate "document.documentElement.scrollWidth<=innerWidth+1")){throw 'Mobile managed profile overflow.'}
    Capture 'team-profile-mobile.png'
    if($script:browserErrors.Count){throw ("JavaScript errors: "+($script:browserErrors -join ', '))}
    Write-Output ("Team desktop/mobile, keyboard search, pending queue and private profile controls: PASS; screenshots="+$OutputDirectory)
} finally {
    if($socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open){try{Send-Cdp 'Browser.close' | Out-Null}catch{}}
    if($socket){$socket.Dispose()}
    if($browserProcess){if(!$browserProcess.WaitForExit(4000)){Stop-Process -Id $browserProcess.Id -Force -ErrorAction SilentlyContinue}}
    if([IO.Path]::GetFullPath((Split-Path -Parent $profile)).TrimEnd('\') -eq [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') -and (Split-Path -Leaf $profile).StartsWith('hrm-profile-browser-')){
        Remove-Item -LiteralPath $profile -Recurse -Force -ErrorAction SilentlyContinue
    }
}
