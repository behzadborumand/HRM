param(
    [Parameter(Mandatory = $true)][string]$AppExe,
    [string]$DotnetRoot,
    [string]$Database = 'HRMS_Next_Trial_20261108',
    [int]$Port = 52321
)

$ErrorActionPreference = 'Stop'
if ($Database -ne 'HRMS_Next_Trial_20261108') { throw 'This smoke test is restricted to the named trial database.' }
$AppExe = (Resolve-Path -LiteralPath $AppExe).Path
if ($DotnetRoot) {
    $DotnetRoot = (Resolve-Path -LiteralPath $DotnetRoot).Path
    $env:DOTNET_ROOT = $DotnetRoot
    $env:DOTNET_ROOT_X64 = $DotnetRoot
}
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
    $leave = Invoke-WebRequest "$baseUrl/Leave" -WebSession $webSession -UseBasicParsing
    $leaveToken = Get-FormToken $leave.Content
    $policyCreated = Invoke-WebRequest "$baseUrl/Leave?handler=Policy" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        Name = 'Annual smoke'; AnnualHours = '240'; __RequestVerificationToken = $leaveToken
    }
    $leave = Invoke-WebRequest "$baseUrl/Leave" -WebSession $webSession -UseBasicParsing
    $policyId = [regex]::Match($leave.Content, '<option value="([1-9][0-9]*)">Annual smoke</option>').Groups[1].Value
    if (!$policyId) { throw 'New Leave policy was not listed.' }
    $leaveToken = Get-FormToken $leave.Content
    $employeeId = [regex]::Match($profilePath, '/Employees/([0-9]+)').Groups[1].Value
    $requested = Invoke-WebRequest "$baseUrl/Leave?handler=Request" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        EmployeeId = $employeeId; PolicyId = $policyId; StartDateJalali = '1403/02/01'; EndDateJalali = '1403/02/02'; Reason = 'Smoke'; __RequestVerificationToken = $leaveToken
    }
    $leave = Invoke-WebRequest "$baseUrl/Leave" -WebSession $webSession -UseBasicParsing
    $requestId = [regex]::Match($leave.Content, 'data-request-id="([1-9][0-9]*)"').Groups[1].Value
    if (!$requestId -or [System.Net.WebUtility]::HtmlDecode($leave.Content) -notmatch 'در انتظار تأیید') { throw 'Leave request was not listed as Pending.' }
    $leaveToken = Get-FormToken $leave.Content
    $approved = Invoke-WebRequest "$baseUrl/Leave?handler=Approve" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        RequestId = $requestId; Comments = 'Smoke approved'; __RequestVerificationToken = $leaveToken
    }
    $leave = Invoke-WebRequest "$baseUrl/Leave" -WebSession $webSession -UseBasicParsing
    if ([System.Net.WebUtility]::HtmlDecode($leave.Content) -notmatch 'تأیید شده') { throw 'Leave request was not approved.' }
    $training = Invoke-WebRequest "$baseUrl/Training" -WebSession $webSession -UseBasicParsing
    $trainingToken = Get-FormToken $training.Content
    $courseCreated = Invoke-WebRequest "$baseUrl/Training?handler=Course" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        Title = 'Safety smoke'; RenewalDays = '365'; __RequestVerificationToken = $trainingToken
    }
    $training = Invoke-WebRequest "$baseUrl/Training" -WebSession $webSession -UseBasicParsing
    $courseId = [regex]::Match($training.Content, '<option value="([1-9][0-9]*)">Safety smoke</option>').Groups[1].Value
    if (!$courseId) { throw 'New Training course was not listed.' }
    $editPage = Invoke-WebRequest "$baseUrl/Training?edit=$courseId" -WebSession $webSession -UseBasicParsing
    $editToken = Get-FormToken $editPage.Content
    if ($editPage.Content -notmatch 'name="EditCourseId"') { throw 'Training edit form was not rendered.' }
    $editedCourse = Invoke-WebRequest "$baseUrl/Training?handler=Course" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        EditCourseId = $courseId; Title = 'Safety smoke edited'; Category = 'Safety'; RenewalDays = '180'; MandatoryForAll = 'true'; __RequestVerificationToken = $editToken
    }
    $training = Invoke-WebRequest "$baseUrl/Training" -WebSession $webSession -UseBasicParsing
    if ($training.Content -notmatch ('<option value="' + $courseId + '">Safety smoke edited</option>')) {
        throw 'Edited Training course was not listed.'
    }
    $trainingToken = Get-FormToken $training.Content
    $assigned = Invoke-WebRequest "$baseUrl/Training?handler=Assign" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        EmployeeId = $employeeId; CourseId = $courseId; AssignedDateJalali = '1403/02/01'; DueDateJalali = '1403/02/05'; __RequestVerificationToken = $trainingToken
    }
    $training = Invoke-WebRequest "$baseUrl/Training" -WebSession $webSession -UseBasicParsing
    $assignmentId = [regex]::Match($training.Content, 'data-assignment-id="([1-9][0-9]*)"').Groups[1].Value
    if (!$assignmentId) { throw 'New Training assignment was not listed.' }
    $trainingToken = Get-FormToken $training.Content
    $completed = Invoke-WebRequest "$baseUrl/Training?handler=Complete" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        AssignmentId = $assignmentId; CompletedDateJalali = '1403/02/02'; CertificateNumber = 'SMOKE-CERT'; __RequestVerificationToken = $trainingToken
    }
    $training = Invoke-WebRequest "$baseUrl/Training" -WebSession $webSession -UseBasicParsing
    if ([System.Net.WebUtility]::HtmlDecode($training.Content) -notmatch 'انجام شده' -or $training.Content -notmatch 'SMOKE-CERT') {
        throw 'Training completion was not listed.'
    }
    $trainingToken = Get-FormToken $training.Content
    $deactivated = Invoke-WebRequest "$baseUrl/Training?handler=ToggleCourse" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        CourseId = $courseId; Active = 'false'; __RequestVerificationToken = $trainingToken
    }
    $training = Invoke-WebRequest "$baseUrl/Training" -WebSession $webSession -UseBasicParsing
    if ($training.Content -match ('<option value="' + $courseId + '">Safety smoke edited</option>')) {
        throw 'Inactive Training course remains available for assignment.'
    }
    $trainingToken = Get-FormToken $training.Content
    $reactivated = Invoke-WebRequest "$baseUrl/Training?handler=ToggleCourse" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        CourseId = $courseId; Active = 'true'; __RequestVerificationToken = $trainingToken
    }
    $training = Invoke-WebRequest "$baseUrl/Training" -WebSession $webSession -UseBasicParsing
    if ($training.Content -notmatch ('<option value="' + $courseId + '">Safety smoke edited</option>')) {
        throw 'Reactivated Training course was not available.'
    }
    $documentsPage = Invoke-WebRequest "$baseUrl/Employees/$employeeId/Documents" -WebSession $webSession -UseBasicParsing
    if ($documentsPage.Content -notmatch 'name="UploadFile"') { throw 'Documents page did not load.' }
    $documentToken = Get-FormToken $documentsPage.Content
    $boundary = '----HRM' + [Guid]::NewGuid().ToString('N')
    $newline = [Environment]::NewLine
    $fileBytes = [Text.Encoding]::ASCII.GetBytes('%PDF-1.4 Smoke document')
    $header = '--' + $boundary + $newline +
        'Content-Disposition: form-data; name="Title"' + $newline + $newline + 'Smoke contract' + $newline +
        '--' + $boundary + $newline +
        'Content-Disposition: form-data; name="Category"' + $newline + $newline + 'HR' + $newline +
        '--' + $boundary + $newline +
        'Content-Disposition: form-data; name="__RequestVerificationToken"' + $newline + $newline + $documentToken + $newline +
        '--' + $boundary + $newline +
        'Content-Disposition: form-data; name="UploadFile"; filename="smoke.pdf"' + $newline +
        'Content-Type: application/pdf' + $newline + $newline
    $tail = $newline + '--' + $boundary + '--' + $newline
    $multipart = [Collections.Generic.List[byte]]::new()
    $multipart.AddRange([Text.Encoding]::UTF8.GetBytes($header))
    $multipart.AddRange($fileBytes)
    $multipart.AddRange([Text.Encoding]::UTF8.GetBytes($tail))
    $uploadBodyPath = Join-Path $env:TEMP ('hrm-multipart-' + $suffix)
    [IO.File]::WriteAllBytes($uploadBodyPath, $multipart.ToArray())
    $cookieHeader = ($webSession.Cookies.GetCookies([Uri]$baseUrl) | ForEach-Object { $_.Name + '=' + $_.Value }) -join '; '
    try {
        $uploadRaw = & curl.exe --insecure --silent --show-error --include --max-time 20 -X POST -H ("Cookie: " + $cookieHeader) -H ("Content-Type: multipart/form-data; boundary=" + $boundary) --data-binary ("@" + $uploadBodyPath) "$baseUrl/Employees/$employeeId/Documents?handler=Upload"
        if ($uploadRaw[0] -notmatch 'HTTP/1.1 302' -or ($uploadRaw -join [Environment]::NewLine) -notmatch 'Location: /Employees/[0-9]+/Documents') { throw 'Document upload did not redirect to the private list.' }
        if ($LASTEXITCODE -ne 0) { throw 'curl upload failed.' }
    } finally {
        Remove-Item -LiteralPath $uploadBodyPath -ErrorAction SilentlyContinue
    }
    $documentsPage = Invoke-WebRequest "$baseUrl/Employees/$employeeId/Documents" -WebSession $webSession -UseBasicParsing
    $documentId = [regex]::Match($documentsPage.Content, 'data-document-id="([1-9][0-9]*)"').Groups[1].Value
    if (!$documentId -or $documentsPage.Content -notmatch 'Smoke contract') { throw 'Uploaded document was not listed.' }
    $profileWithDocument = Invoke-WebRequest "$baseUrl/Employees/$employeeId" -WebSession $webSession -UseBasicParsing
    if ($profileWithDocument.Content -notmatch ('data-document-id="' + $documentId + '"')) { throw 'Uploaded document was not shown in the employee profile tab.' }
    $downloadFile = Join-Path $env:TEMP ('hrm-smoke-document-' + $suffix + '.pdf')
    try {
        Invoke-WebRequest "$baseUrl/Employees/$employeeId/Documents?handler=Download&id=$documentId" -WebSession $webSession -UseBasicParsing -OutFile $downloadFile | Out-Null
        if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($downloadFile)) -ne [Convert]::ToBase64String($fileBytes)) {
            throw 'Private document download bytes differ from upload.'
        }
    } finally {
        Remove-Item -LiteralPath $downloadFile -ErrorAction SilentlyContinue
    }
    $documentToken = Get-FormToken $documentsPage.Content
    $deletedDocument = Invoke-WebRequest "$baseUrl/Employees/$employeeId/Documents?handler=Delete" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        DocumentId = $documentId; __RequestVerificationToken = $documentToken
    }
    $documentsPage = Invoke-WebRequest "$baseUrl/Employees/$employeeId/Documents" -WebSession $webSession -UseBasicParsing
    if ($documentsPage.Content -match 'data-document-id=') { throw 'Deleted document remains visible.' }
    Write-Output 'HTTPS login, employee, Leave, Training, Documents: PASS'
}
finally {
    if ($process) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    $cleanup = "SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; SET QUOTED_IDENTIFIER ON; DECLARE @id int=(SELECT Id FROM organization.Companies WHERE Name='$companyName'); DELETE FROM training.Assignments WHERE CompanyId=@id; DELETE FROM training.Courses WHERE CompanyId=@id; DELETE FROM leave.Requests WHERE CompanyId=@id; DELETE FROM leave.Balances WHERE CompanyId=@id; DELETE FROM leave.Policies WHERE CompanyId=@id; DELETE FROM documents.EmployeeDocuments WHERE CompanyId=@id; DELETE FROM documents.DocumentAudits WHERE CompanyId=@id; DELETE FROM personnel.EmployeeAudits WHERE EmployeeId IN (SELECT Id FROM personnel.Employees WHERE CompanyId=@id); DELETE FROM personnel.Employees WHERE CompanyId=@id; DELETE FROM access.Users WHERE CompanyId=@id; DELETE FROM organization.Departments WHERE CompanyId=@id; DELETE FROM organization.Companies WHERE Id=@id;"
    $cleanupOutput = sqlcmd -S . -E -C -b -d $Database -Q $cleanup 2>&1
    $cleanupExit = $LASTEXITCODE
    Remove-Item Env:ConnectionStrings__HrmsNext,Env:HRMS_BOOTSTRAP_USER,Env:HRMS_BOOTSTRAP_PASSWORD,Env:HRMS_BOOTSTRAP_COMPANY,Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
    if ($cleanupExit -ne 0) { throw "Smoke cleanup failed: $cleanupOutput" }
}
