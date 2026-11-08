param(
    [Parameter(Mandatory = $true)][string]$AppExe,
    [string]$Database = 'HRMS_Next_Trial_20261108',
    [int]$Port = 52321
)

$ErrorActionPreference = 'Stop'
if ($Database -ne 'HRMS_Next_Trial_20261108') { throw 'This smoke test is restricted to the named trial database.' }
$AppExe = (Resolve-Path -LiteralPath $AppExe).Path
$suffix = [Guid]::NewGuid().ToString('N')
$username = 'smoke_' + $suffix.Substring(0, 12)
$password = [Guid]::NewGuid().ToString('N') + 'A1!'
$companyName = 'Smoke_' + $suffix
$employeeNumber = 'SM_' + $suffix.Substring(0, 12)
$baseUrl = "https://localhost:$Port"
$process = $null

function Get-FormToken([string]$html) {
    $tag = [regex]::Match($html, '<input[^>]*name="__RequestVerificationToken"[^>]*>')
    $token = [regex]::Match($tag.Value, 'value="([^"]+)"').Groups[1].Value
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'Antiforgery token was not rendered.' }
    return $token
}

try {
    $env:ConnectionStrings__HrmsNext = "Server=.;Database=$Database;Integrated Security=True;Encrypt=False;TrustServerCertificate=True"
    $env:HRMS_BOOTSTRAP_USER = $username
    $env:HRMS_BOOTSTRAP_PASSWORD = $password
    $env:HRMS_BOOTSTRAP_COMPANY = $companyName
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    Push-Location (Split-Path -Parent $AppExe)
    try {
        & $AppExe --bootstrap
        if ($LASTEXITCODE -ne 0) { throw "Bootstrap exited with $LASTEXITCODE" }
    } finally { Pop-Location }

    $process = Start-Process -FilePath $AppExe -ArgumentList '--urls', $baseUrl -WorkingDirectory (Split-Path -Parent $AppExe) -WindowStyle Hidden -PassThru
    $login = $null
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try { $login = Invoke-WebRequest "$baseUrl/Login" -SessionVariable webSession -UseBasicParsing; break }
        catch { Start-Sleep -Milliseconds 500 }
    }
    if ($null -eq $login) { throw 'HTTPS host did not start.' }
    $loginToken = Get-FormToken $login.Content
    $employeeListResponse = Invoke-WebRequest "$baseUrl/Login" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        Username = $username; Password = $password; __RequestVerificationToken = $loginToken
    }
    if ($employeeListResponse.BaseResponse.ResponseUri.AbsolutePath -ne '/Employees') { throw 'Login did not reach employee list.' }

    $create = Invoke-WebRequest "$baseUrl/Employees/Create" -WebSession $webSession -UseBasicParsing
    $createToken = Get-FormToken $create.Content
    $departmentId = [regex]::Match($create.Content, '<option value="([1-9][0-9]*)">').Groups[1].Value
    if (!$departmentId) { throw 'Bootstrap department was not offered in the form.' }
    $profile = Invoke-WebRequest "$baseUrl/Employees/Create" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        'Form.EmployeeNumber' = $employeeNumber
        'Form.FirstName' = 'Smoke'
        'Form.LastName' = 'Created'
        'Form.DepartmentId' = $departmentId
        'Form.HireDateJalali' = '1403/01/01'
        __RequestVerificationToken = $createToken
    }
    $profilePath = $profile.BaseResponse.ResponseUri.AbsolutePath
    if ($profilePath -notmatch '^/Employees/[0-9]+$') {
        $messages = [regex]::Matches($profile.Content, '<li>[^<]*</li>') | ForEach-Object { $_.Value }
        throw "Create returned $profilePath; validation: $($messages -join '; ')"
    }

    $edit = Invoke-WebRequest "$baseUrl$profilePath/Edit" -WebSession $webSession -UseBasicParsing
    $editToken = Get-FormToken $edit.Content
    $versionTag = [regex]::Match($edit.Content, '<input[^>]*name="Form.Version"[^>]*>')
    $version = [regex]::Match($versionTag.Value, 'value="([^"]+)"').Groups[1].Value
    if (!$version) { throw 'Concurrency token was not rendered.' }
    $updated = Invoke-WebRequest "$baseUrl$profilePath/Edit" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        'Form.EmployeeNumber' = $employeeNumber
        'Form.FirstName' = 'Smoke'
        'Form.LastName' = 'Edited'
        'Form.DepartmentId' = $departmentId
        'Form.HireDateJalali' = '1403/01/01'
        'Form.Version' = $version
        __RequestVerificationToken = $editToken
    }
    if ($updated.BaseResponse.ResponseUri.AbsolutePath -ne $profilePath -or $updated.Content -notmatch 'Edited') {
        $messages = [regex]::Matches($updated.Content, '<li>[^<]*</li>') | ForEach-Object { [System.Net.WebUtility]::HtmlDecode($_.Value) }
        throw "Edit returned $($updated.BaseResponse.ResponseUri.AbsolutePath); validation: $($messages -join '; ')"
    }
    Write-Output 'HTTPS login, employee create, profile and edit: PASS'
}
finally {
    if ($process) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    $cleanup = "DECLARE @id int=(SELECT Id FROM organization.Companies WHERE Name='$companyName'); DELETE FROM personnel.EmployeeAudits WHERE EmployeeId IN (SELECT Id FROM personnel.Employees WHERE CompanyId=@id); DELETE FROM personnel.Employees WHERE CompanyId=@id; DELETE FROM access.Users WHERE CompanyId=@id; DELETE FROM organization.Departments WHERE CompanyId=@id; DELETE FROM organization.Companies WHERE Id=@id;"
    sqlcmd -S . -E -C -b -d $Database -Q $cleanup | Out-Null
    Remove-Item Env:ConnectionStrings__HrmsNext,Env:HRMS_BOOTSTRAP_USER,Env:HRMS_BOOTSTRAP_PASSWORD,Env:HRMS_BOOTSTRAP_COMPANY,Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
}
