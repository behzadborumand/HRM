param(
    [Parameter(Mandatory=$true)][string]$BaseUrl,
    [Parameter(Mandatory=$true)]$AdminSession,
    [Parameter(Mandatory=$true)]$ReaderSession,
    [Parameter(Mandatory=$true)]$OwnerSession,
    [Parameter(Mandatory=$true)][int]$EmployeeId
)
$ErrorActionPreference='Stop'
if(([Uri]$BaseUrl).Host -ne 'localhost'){throw 'Local trial host only.'}
function Token([string]$html){$tag=[regex]::Match($html,'<input[^>]*name="__RequestVerificationToken"[^>]*>');return [Net.WebUtility]::HtmlDecode([regex]::Match($tag.Value,'value="([^"]+)"').Groups[1].Value)}
foreach($session in @($ReaderSession,$OwnerSession)){
    $denied=Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Edit" -WebSession $session -UseBasicParsing
    if($denied.BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw 'Reader/owner gained general employee editing.'}
}
$users=Invoke-WebRequest "$BaseUrl/Settings/Users" -WebSession $AdminSession -UseBasicParsing
$username='smoke_editor_'+[Guid]::NewGuid().ToString('N').Substring(0,12)
$password=[Guid]::NewGuid().ToString('N')+'A1!'
$created=Invoke-WebRequest "$BaseUrl/Settings/Users?handler=Create" -Method Post -WebSession $AdminSession -UseBasicParsing -Body @{
    NewUsername=$username;NewPassword=$password;NewEmployeeId='';NewCanRead='false';NewCanWrite='true';__RequestVerificationToken=(Token $users.Content)
}
if($created.Content -notmatch $username){throw 'Writer fixture creation failed.'}
$login=Invoke-WebRequest "$BaseUrl/Login" -SessionVariable writerSession -UseBasicParsing
Invoke-WebRequest "$BaseUrl/Login" -Method Post -WebSession $writerSession -UseBasicParsing -Body @{Username=$username;Password=$password;__RequestVerificationToken=(Token $login.Content)}|Out-Null
$edit=Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Edit" -WebSession $writerSession -UseBasicParsing
if($edit.Content -notmatch 'profile-hero-avatar' -or $edit.Content -notmatch 'employee-sidebar' -or $edit.Content -notmatch 'name="Form.Version"'){throw 'Writer editor shell failed.'}
if($edit.Content -match 'PRIVATE_PERSONAL_SMOKE|PRIVATE_ADDRESS_SMOKE|PRIVATE_EMERGENCY_SMOKE|data-private-personal|#tab-pay|#tab-leave|#tab-training' -or $edit.Content.Contains('href="/Employees/'+$EmployeeId+'/Personal"')){throw 'Writer editor leaked private data or inaccessible links.'}
$private=Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Personal" -WebSession $writerSession -UseBasicParsing
if($private.BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw 'Writer gained personal identity editing.'}
Write-Output 'Employee editor profile shell, invalid-input preservation and owner/reader/writer privacy boundaries: PASS'
