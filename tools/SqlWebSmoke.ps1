param(
    [Parameter(Mandatory = $true)][string]$AppExe,
    [string]$DotnetRoot,
    [string]$Database = 'HRMS_Next_Trial_20261108',
    [int]$Port = 52321,
    [string]$ScreenshotsDirectory
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
    if ($employeeListResponse.BaseResponse.ResponseUri.AbsolutePath -ne '/' -or $employeeListResponse.Content -notmatch 'home-workspace') { throw 'Login did not reach Home.' }
    if ($employeeListResponse.Content -notmatch 'home-availability' -or $employeeListResponse.Content -notmatch 'home-new-hires' -or $employeeListResponse.Content -notmatch 'home-organization') { throw 'Home highlights did not render.' }

    $settings = Invoke-WebRequest "$baseUrl/Settings" -WebSession $webSession -UseBasicParsing
    if ($settings.Content -notmatch 'واحدهای سازمانی') { throw 'Settings hub did not render departments.' }
    $departments = Invoke-WebRequest "$baseUrl/Settings/Departments" -WebSession $webSession -UseBasicParsing
    $departmentToken = Get-FormToken $departments.Content
    $departmentCreated = Invoke-WebRequest "$baseUrl/Settings/Departments?handler=Save" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        Name = 'Smoke unit'; ParentId = ''; EditId = '0'; Version = ''; __RequestVerificationToken = $departmentToken
    }
    if ($departmentCreated.Content -notmatch 'Smoke unit') { throw 'Department administration did not save the test unit.' }
    $links = Invoke-WebRequest "$baseUrl/Settings/OrganizationLinks" -WebSession $webSession -UseBasicParsing
    $linkToken = Get-FormToken $links.Content
    $invalidLink = Invoke-WebRequest "$baseUrl/Settings/OrganizationLinks?handler=Save" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        Title = 'Bad smoke'; LinkUrl = 'javascript:alert(1)'; Description = ''; SortOrder = '0'; Active = 'true'; EditId = '0'; Version = ''; __RequestVerificationToken = $linkToken
    }
    if ($invalidLink.BaseResponse.ResponseUri.AbsolutePath -ne '/Settings/OrganizationLinks' -or $invalidLink.Content -match 'data-link-id=') { throw 'Unsafe organization link was accepted.' }
    $linkCreated = Invoke-WebRequest "$baseUrl/Settings/OrganizationLinks?handler=Save" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        Title = 'Smoke help'; LinkUrl = 'https://example.com/help'; Description = 'Smoke'; SortOrder = '1'; Active = 'true'; EditId = '0'; Version = ''; __RequestVerificationToken = $linkToken
    }
    if ($linkCreated.Content -notmatch 'Smoke help') { throw 'Organization link did not save.' }
    $homeResponse = Invoke-WebRequest "$baseUrl/" -WebSession $webSession -UseBasicParsing
    if ($homeResponse.Content -notmatch 'Smoke help' -or $homeResponse.Content -notmatch 'rel="noopener noreferrer"') { throw 'Active organization link was not rendered safely on Home.' }
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
    $version = [System.Net.WebUtility]::HtmlDecode([regex]::Match($versionTag.Value, 'value="([^"]+)"').Groups[1].Value)
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
    $employeeId = [regex]::Match($profilePath, '/Employees/([0-9]+)').Groups[1].Value
    $accounts = Invoke-WebRequest "$baseUrl/Settings/Users" -WebSession $webSession -UseBasicParsing
    if ($accounts.Content -notmatch 'حساب‌های کاربری') { throw 'Account administration did not load.' }
    $accountToken = Get-FormToken $accounts.Content
    $memberUsername = 'smoke_member_' + $suffix.Substring(0, 10)
    $memberPassword = [Guid]::NewGuid().ToString('N') + 'A1!'
    $createdAccount = Invoke-WebRequest "$baseUrl/Settings/Users?handler=Create" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        NewUsername = $memberUsername; NewPassword = $memberPassword; NewEmployeeId = $employeeId
        NewCanRead = 'false'; __RequestVerificationToken = $accountToken
    }
    if ($createdAccount.Content -notmatch $memberUsername -or $createdAccount.Content -notmatch "پروندهٔ $employeeId") { $messages = [regex]::Matches($createdAccount.Content,'<li>[^<]*</li>') | ForEach-Object { [Net.WebUtility]::HtmlDecode($_.Value) }; throw ('Employee-linked account was not created: ' + ($messages -join ' ')) }
    $memberLogin = Invoke-WebRequest "$baseUrl/Login" -SessionVariable memberSession -UseBasicParsing
    $memberToken = Get-FormToken $memberLogin.Content
    $memberHome = Invoke-WebRequest "$baseUrl/Login" -Method Post -WebSession $memberSession -UseBasicParsing -Body @{
        Username = $memberUsername; Password = $memberPassword; __RequestVerificationToken = $memberToken
    }
    if ($memberHome.BaseResponse.ResponseUri.AbsolutePath -ne '/' -or $memberHome.Content -notmatch $memberUsername) { throw 'Employee-linked account could not sign in.' }    $memberProfile = Invoke-WebRequest "$baseUrl$profilePath" -WebSession $memberSession -UseBasicParsing
    try { $myInfo = Invoke-WebRequest "$baseUrl/MyInfo" -WebSession $memberSession -UseBasicParsing } catch { throw 'MyInfo self-service route is unavailable.' }
    if ($myInfo.BaseResponse.ResponseUri.AbsolutePath -ne $profilePath -or $myInfo.Content -notmatch 'data-profile-leave') { throw 'MyInfo did not resolve the linked employee without directory grants.' }
    if ($memberHome.Content -match 'role="search"|href="/Employees"') { throw 'Owner-only account sees the company directory controls.' }
    if ($memberProfile.Content -notmatch 'data-profile-leave' -or $memberProfile.Content -notmatch 'data-profile-training') { throw 'Own Leave/Training profile tabs were not rendered.' }
    if ($memberProfile.Content -notmatch 'data-pay-history') { throw 'Own pay-history tab was not available to the linked employee.' }
    $readerUsername = 'smoke_reader_' + $suffix.Substring(0, 10)
    $readerPassword = [Guid]::NewGuid().ToString('N') + 'A1!'
    $createdReader = Invoke-WebRequest "$baseUrl/Settings/Users?handler=Create" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        NewUsername=$readerUsername; NewPassword=$readerPassword; NewEmployeeId=''
        NewCanRead='true'; __RequestVerificationToken=$accountToken
    }
    if ($createdReader.Content -notmatch $readerUsername) { throw 'Unlinked reader account creation failed.' }
    $readerLogin = Invoke-WebRequest "$baseUrl/Login" -SessionVariable readerSession -UseBasicParsing
    $readerHome = Invoke-WebRequest "$baseUrl/Login" -Method Post -WebSession $readerSession -UseBasicParsing -Body @{
        Username=$readerUsername; Password=$readerPassword; __RequestVerificationToken=(Get-FormToken $readerLogin.Content)
    }
    $readerProfile = Invoke-WebRequest "$baseUrl$profilePath" -WebSession $readerSession -UseBasicParsing
    if ($readerProfile.Content -match 'data-pay-history|profile-pay-tab|نرخ ثبت‌شده|data-profile-leave|data-profile-training|profile-leave-tab|profile-training-tab') { throw 'Pay data leaked into the general reader profile.' }
    $titles = Invoke-WebRequest "$baseUrl/Settings/JobTitles" -WebSession $webSession -UseBasicParsing
    $titleToken = Get-FormToken $titles.Content
    $titleCreated = Invoke-WebRequest "$baseUrl/Settings/JobTitles?handler=Save" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        Name = 'Smoke engineer'; Description = 'Smoke test'; EditId = '0'; Version = ''; __RequestVerificationToken = $titleToken
    }
    if ($titleCreated.Content -notmatch 'Smoke engineer') { throw 'Title catalog did not save the test title.' }
    $jobEdit = Invoke-WebRequest "$baseUrl$profilePath/Edit" -WebSession $webSession -UseBasicParsing
    $jobVersionTag = [regex]::Match($jobEdit.Content, '<input[^>]*name="JobForm.Version"[^>]*>')
    $jobVersion = [System.Net.WebUtility]::HtmlDecode([regex]::Match($jobVersionTag.Value, 'value="([^"]+)"').Groups[1].Value)
    if (!$jobVersion) { throw 'Job change form did not render.' }
    $jobToken = Get-FormToken $jobEdit.Content
    $invalidManager = Invoke-WebRequest "$baseUrl$profilePath/Edit?handler=Job" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        'JobForm.DepartmentId' = $departmentId
        'JobForm.JobTitle' = 'Invalid manager smoke'
        'JobForm.ManagerEmployeeId' = 'not-a-number'
        'JobForm.EmploymentType' = 'FullTime'
        'JobForm.EffectiveDateJalali' = '1403/01/01'
        'JobForm.Version' = $jobVersion
        __RequestVerificationToken = $jobToken
    }
    if ($invalidManager.BaseResponse.ResponseUri.AbsolutePath -ne "$profilePath/Edit") {
        throw 'Nonnumeric manager value was accepted by the job form.'
    }
    $jobChanged = Invoke-WebRequest "$baseUrl$profilePath/Edit?handler=Job" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        'JobForm.DepartmentId' = $departmentId
        'JobForm.JobTitle' = 'Smoke engineer'
        'JobForm.ManagerEmployeeId' = ''
        'JobForm.EmploymentType' = 'FullTime'
        'JobForm.EffectiveDateJalali' = '1403/01/01'
        'JobForm.Version' = $jobVersion
        __RequestVerificationToken = $jobToken
    }
    if ($jobChanged.BaseResponse.ResponseUri.AbsolutePath -ne $profilePath -or
        $jobChanged.Content -notmatch 'data-pay-history' -or
        $jobChanged.Content -notmatch 'data-job-id="[1-9][0-9]*"' -or
        $jobChanged.Content -notmatch 'Smoke engineer') {
        $errors = [regex]::Matches($jobChanged.Content, '<li>[^<]*</li>') | ForEach-Object { [System.Net.WebUtility]::HtmlDecode($_.Value) }
        throw "Job change returned $($jobChanged.BaseResponse.ResponseUri.AbsolutePath); validation: $($errors -join '; ')"
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
    $reports = Invoke-WebRequest "$baseUrl/Reports" -WebSession $webSession -UseBasicParsing
    if ($reports.Content -notmatch 'گزارش لیست کارکنان' -or $reports.Content -notmatch 'گزارش خلاصه مرخصی') { throw 'Reports hub did not load.' }
    $employeeReport = Invoke-WebRequest "$baseUrl/Reports/Employees?keyword=$employeeNumber" -WebSession $webSession -UseBasicParsing
    if ($employeeReport.Content -notmatch $employeeNumber -or $employeeReport.Content -match '<th>حقوق</th>') { throw 'Scoped employee report is invalid or exposes pay.' }
    $employeeCsv = Invoke-WebRequest "$baseUrl/Reports/Employees?handler=Export&keyword=$employeeNumber" -WebSession $webSession -UseBasicParsing
    if ($employeeCsv.Headers['Content-Type'] -notmatch 'text/csv' -or $employeeCsv.Content -notmatch $employeeNumber) { throw 'Employee CSV export failed.' }
    $leaveReport = Invoke-WebRequest "$baseUrl/Reports/Leave?year=2024" -WebSession $webSession -UseBasicParsing
    if ($leaveReport.Content -notmatch 'Annual smoke') { throw 'Leave summary report did not show the test policy.' }
    $leaveCsv = Invoke-WebRequest "$baseUrl/Reports/Leave?handler=Export&year=2024" -WebSession $webSession -UseBasicParsing
    if ($leaveCsv.Headers['Content-Type'] -notmatch 'text/csv' -or $leaveCsv.Content -notmatch 'Annual smoke') { throw 'Leave CSV export failed.' }
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
    foreach ($activitySession in @($webSession, $memberSession)) {
        $activityProfile = Invoke-WebRequest "$baseUrl$profilePath" -WebSession $activitySession -UseBasicParsing
        if ($activityProfile.Content -notmatch ('data-profile-request-id="' + $requestId + '"') -or
            $activityProfile.Content -notmatch ('data-profile-assignment-id="' + $assignmentId + '"') -or
            $activityProfile.Content -notmatch 'Safety smoke edited' -or $activityProfile.Content -notmatch 'Annual smoke') {
            throw 'Real Leave/Training records were not projected into the authorized employee profile.'
        }
        if ($activityProfile.Content -match 'SMOKE-CERT|Smoke approved') { throw 'Private certificate or decision comments leaked into profile activity.' }
    }
    $ownerActivityProfile = Invoke-WebRequest "$baseUrl$profilePath" -WebSession $memberSession -UseBasicParsing
    if ($ownerActivityProfile.Content -match 'href="/Leave"|href="/Training"') { throw 'Owner activity view exposes administrator-only management links.' }
    $readerActivityProfile = Invoke-WebRequest "$baseUrl$profilePath" -WebSession $readerSession -UseBasicParsing
    if ($readerActivityProfile.Content -match 'data-profile-request-id|data-profile-assignment-id|Annual smoke|Safety smoke edited') {
        throw 'Private employee activity leaked to a general reader.'
    }
    $peerCreate = Invoke-WebRequest "$baseUrl/Employees/Create" -WebSession $webSession -UseBasicParsing
    $peerResult = Invoke-WebRequest "$baseUrl/Employees/Create" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        'Form.EmployeeNumber'=($employeeNumber+'_P'); 'Form.FirstName'='Private'; 'Form.LastName'='Peer'
        'Form.DepartmentId'=$departmentId; 'Form.HireDateJalali'='1403/01/01'; __RequestVerificationToken=(Get-FormToken $peerCreate.Content)
    }
    $peerPath=$peerResult.BaseResponse.ResponseUri.AbsolutePath
    $peerId=[regex]::Match($peerPath,'/Employees/([0-9]+)$').Groups[1].Value
    if(!$peerId){throw 'Self-service peer fixture was not created.'}
    foreach($deniedPath in @('/Employees', "$peerPath", "$peerPath/Photo", "$peerPath/Documents", "$profilePath/Edit", '/Leave', '/Training', '/Settings/Users', '/Reports')) {
        $denied = Invoke-WebRequest ($baseUrl+$deniedPath) -WebSession $memberSession -UseBasicParsing
        if($denied.BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw "Owner accessed a forbidden route: $deniedPath"}
    }
    foreach($unlinkedPath in @('/MyInfo','/My/Leave')) {
        $denied = Invoke-WebRequest ($baseUrl+$unlinkedPath) -WebSession $readerSession -UseBasicParsing
        if($denied.BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw 'Unlinked reader accessed employee self-service.'}
    }
    $ownLeave = Invoke-WebRequest "$baseUrl/My/Leave" -WebSession $memberSession -UseBasicParsing
    if($ownLeave.Content -notmatch 'data-own-leave'){throw 'Personal Leave form was not rendered.'}
    $ownToken=Get-FormToken $ownLeave.Content
    $invalidOwn=Invoke-WebRequest "$baseUrl/My/Leave?handler=Request" -Method Post -WebSession $memberSession -UseBasicParsing -Body @{
        PolicyId=$policyId; StartDateJalali='bad'; EndDateJalali='bad'; __RequestVerificationToken=$ownToken
    }
    if([Net.WebUtility]::HtmlDecode($invalidOwn.Content) -notmatch 'تاریخ شروع و پایان شمسی معتبر'){throw 'Personal Leave invalid dates were not explained in Persian.'}
    $ownRequested=Invoke-WebRequest "$baseUrl/My/Leave?handler=Request" -Method Post -WebSession $memberSession -UseBasicParsing -Body @{
        PolicyId=$policyId; EmployeeId=$peerId; CompanyId='2147483647'; StartDateJalali='1403/03/01'; EndDateJalali='1403/03/01'; Reason='Own smoke'; __RequestVerificationToken=$ownToken
    }
    $ownRequestId=[regex]::Match($ownRequested.Content,'data-own-request-id="([0-9]+)"').Groups[1].Value
    $ownVersion=[Net.WebUtility]::HtmlDecode([regex]::Match($ownRequested.Content,'name="Version" value="([^"]+)"').Groups[1].Value)
    if(!$ownRequestId -or !$ownVersion){throw 'Own request or cancellation version was not rendered.'}
    $peerAdminProfile=Invoke-WebRequest "$baseUrl$peerPath" -WebSession $webSession -UseBasicParsing
    if($peerAdminProfile.Content -match ('data-profile-request-id="'+$ownRequestId+'"')){throw 'Forged EmployeeId changed another employee.'}
    $ownProfile=Invoke-WebRequest "$baseUrl$profilePath" -WebSession $memberSession -UseBasicParsing
    if($ownProfile.Content -notmatch ('data-profile-request-id="'+$ownRequestId+'"')){throw 'Request was not attached to the authenticated employee.'}
    $approveDenied=Invoke-WebRequest "$baseUrl/Leave?handler=Approve" -Method Post -WebSession $memberSession -UseBasicParsing -Body @{
        RequestId=$ownRequestId; __RequestVerificationToken=$ownToken
    }
    if($approveDenied.BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw 'Owner approved a Leave request.'}
    $stale=Invoke-WebRequest "$baseUrl/My/Leave?handler=Cancel" -Method Post -WebSession $memberSession -UseBasicParsing -Body @{
        RequestId=$ownRequestId; Version='AA=='; __RequestVerificationToken=$ownToken
    }
    if($stale.Content -notmatch ('data-own-request-id="'+$ownRequestId+'"') -or [Net.WebUtility]::HtmlDecode($stale.Content) -notmatch 'درخواست تغییر کرده'){throw 'Stale cancellation was not rejected.'}
    $csrfStatus=$null
    try { Invoke-WebRequest "$baseUrl/My/Leave?handler=Cancel" -Method Post -WebSession $memberSession -UseBasicParsing -Body @{ RequestId=$ownRequestId;Version=$ownVersion } | Out-Null }
    catch { if($_.Exception.Response){$csrfStatus=[int]$_.Exception.Response.StatusCode}else{throw} }
    if($csrfStatus -ne 400){throw 'Missing CSRF token was not rejected.'}
    $ownCancelled=Invoke-WebRequest "$baseUrl/My/Leave?handler=Cancel" -Method Post -WebSession $memberSession -UseBasicParsing -Body @{
        RequestId=$ownRequestId; Version=$ownVersion; __RequestVerificationToken=$ownToken
    }
    if($ownCancelled.Content -match ('data-own-request-id="'+$ownRequestId+'"')){throw 'Cancelled personal request remains in the pending list.'}
    $replay=Invoke-WebRequest "$baseUrl/My/Leave?handler=Cancel" -Method Post -WebSession $memberSession -UseBasicParsing -Body @{
        RequestId=$ownRequestId;Version=$ownVersion;__RequestVerificationToken=$ownToken
    }
    if([Net.WebUtility]::HtmlDecode($replay.Content) -notmatch 'درخواست تغییر کرده'){throw 'Replayed cancellation was accepted.'}
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
        $ownDownloaded=Join-Path $env:TEMP ('hrm-owner-document-'+$suffix+'.pdf')
        try {
            Invoke-WebRequest "$baseUrl/Employees/$employeeId/Documents?handler=Download&id=$documentId" -WebSession $memberSession -UseBasicParsing -OutFile $ownDownloaded | Out-Null
            if([Convert]::ToBase64String([IO.File]::ReadAllBytes($ownDownloaded)) -ne [Convert]::ToBase64String($fileBytes)){throw 'Owner private file download differs.'}
        } finally { Remove-Item -LiteralPath $ownDownloaded -ErrorAction SilentlyContinue }
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

    $photoProfile = Invoke-WebRequest "$baseUrl$profilePath" -WebSession $webSession -UseBasicParsing
    if ($photoProfile.Content -notmatch 'profile-photo-trigger') { throw 'Private photo controls did not render.' }
    $photoToken = Get-FormToken $photoProfile.Content
    $photoBytes = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j4l8AAAAASUVORK5CYII=')
    $photoBoundary = '----HRMPhoto' + $suffix
    $photoHeader = '--' + $photoBoundary + $newline +
        'Content-Disposition: form-data; name="Version"' + $newline + $newline + $newline +
        '--' + $photoBoundary + $newline +
        'Content-Disposition: form-data; name="__RequestVerificationToken"' + $newline + $newline + $photoToken + $newline +
        '--' + $photoBoundary + $newline +
        'Content-Disposition: form-data; name="UploadFile"; filename="portrait.png"' + $newline +
        'Content-Type: image/png' + $newline + $newline
    $photoTail = $newline + '--' + $photoBoundary + '--' + $newline
    $photoMultipart = [Collections.Generic.List[byte]]::new()
    $photoMultipart.AddRange([Text.Encoding]::UTF8.GetBytes($photoHeader))
    $photoMultipart.AddRange($photoBytes)
    $photoMultipart.AddRange([Text.Encoding]::UTF8.GetBytes($photoTail))
    $photoBody = Join-Path $env:TEMP ('hrm-photo-' + $suffix)
    $photoDownload = Join-Path $env:TEMP ('hrm-photo-download-' + $suffix)
    try {
        [IO.File]::WriteAllBytes($photoBody, $photoMultipart.ToArray())
        $photoHeaders = & curl.exe --insecure --silent --show-error --dump-header - --output NUL --max-time 20 -X POST -H ("Cookie: " + $cookieHeader) -H ("Content-Type: multipart/form-data; boundary=" + $photoBoundary) --data-binary ("@" + $photoBody) "$baseUrl$profilePath/Photo?handler=Upload"
        if ($LASTEXITCODE -ne 0 -or ($photoHeaders -join ' ') -notmatch '302') { throw 'Photo upload failed.' }
        $privatePhoto = Invoke-WebRequest "$baseUrl$profilePath/Photo" -WebSession $webSession -UseBasicParsing -OutFile $photoDownload -PassThru
        if ($privatePhoto.Headers['Content-Type'] -notmatch 'image/png' -or [Convert]::ToBase64String([IO.File]::ReadAllBytes($photoDownload)) -ne [Convert]::ToBase64String($photoBytes)) { throw 'Private photo bytes or content type differ.' }
        $anonymousPhoto = Invoke-WebRequest "$baseUrl$profilePath/Photo" -UseBasicParsing
        if ($anonymousPhoto.BaseResponse.ResponseUri.AbsolutePath -ne '/Login') { throw 'Photo was accessible anonymously.' }
    } finally { Remove-Item -LiteralPath $photoBody,$photoDownload -ErrorAction SilentlyContinue }
    $photoProfile = Invoke-WebRequest "$baseUrl$profilePath" -WebSession $webSession -UseBasicParsing
    $photoVersion = [System.Net.WebUtility]::HtmlDecode([regex]::Match($photoProfile.Content, '<input[^>]*name="Version"[^>]*value="([^"]*)"').Groups[1].Value)
    if (!$photoVersion) { throw 'Photo concurrency version did not render.' }
    $photoDeleted = Invoke-WebRequest "$baseUrl$profilePath/Photo?handler=Delete" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        Version=$photoVersion; __RequestVerificationToken=(Get-FormToken $photoProfile.Content)
    }
    if ($photoDeleted.Content -match '<img[^>]+/Photo') { throw 'Deleted photo is still rendered.' }
    & (Join-Path $PSScriptRoot 'TestTeamWorkflow.ps1') -BaseUrl $baseUrl -AdminSession $webSession -CompanyName $companyName -EmployeeId $employeeId -PeerId $peerId -PolicyId $policyId -DepartmentId $departmentId -ScreenshotsDirectory $ScreenshotsDirectory
    if ($ScreenshotsDirectory) {
        $visualFixture = @"
SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; SET QUOTED_IDENTIFIER ON;
DECLARE @company int=(SELECT Id FROM organization.Companies WHERE Name='$companyName');
DECLARE @employee int=$employeeId, @department int=(SELECT DepartmentId FROM personnel.Employees WHERE Id=$employeeId AND CompanyId=@company);
UPDATE personnel.Employees SET FirstName=N'آرمان',LastName=N'نیک‌روش',Email='arman.nikroush@example.test' WHERE Id=@employee AND CompanyId=@company;
UPDATE organization.Departments SET Name=N'توسعهٔ محصول' WHERE Id=@department AND CompanyId=@company;
INSERT personnel.Employees(CompanyId,DepartmentId,EmployeeNumber,FirstName,LastName,HireDate) VALUES(@company,@department,'$($employeeNumber)_M',N'مهدی',N'رضایی','2020-01-01');
DECLARE @manager int=SCOPE_IDENTITY();
INSERT personnel.Employees(CompanyId,DepartmentId,EmployeeNumber,FirstName,LastName,HireDate) VALUES(@company,@department,'$($employeeNumber)_R',N'سارا',N'نادری','2024-01-01');
DECLARE @report int=SCOPE_IDENTITY();
UPDATE personnel.EmployeeJobs SET StartDate='2025-01-01',JobTitle=N'کارشناس ارشد محصول',PayType='Salary',PayRate=720000000,ManagerEmployeeId=@manager WHERE EmployeeId=@employee AND CompanyId=@company AND IsCurrent=1;
INSERT personnel.EmployeeJobs(CompanyId,EmployeeId,DepartmentId,ManagerEmployeeId,JobTitle,EmploymentType,StartDate,EndDate,IsCurrent,PayType,PayRate) VALUES
(@company,@employee,@department,@manager,N'کارشناس محصول','FullTime','2024-09-01','2024-12-31',0,'Salary',600000000),
(@company,@employee,@department,NULL,N'کارشناس برنامه‌ریزی','FullTime','2024-03-20','2024-08-31',0,'Salary',480000000),
(@company,@report,@department,@employee,N'کارشناس محصول','FullTime','2024-01-01',NULL,1,'Salary',360000000);

DECLARE @balanceYear int=DATEPART(year,SYSDATETIMEOFFSET() AT TIME ZONE 'Iran Standard Time');
DECLARE @policy int=(SELECT TOP(1) Id FROM leave.Policies WHERE CompanyId=@company);
DECLARE @course int=(SELECT TOP(1) Id FROM training.Courses WHERE CompanyId=@company);
UPDATE leave.Policies SET Name=N'مرخصی استحقاقی' WHERE Id=@policy AND CompanyId=@company;
UPDATE training.Courses SET Title=N'اصول ایمنی و سلامت محیط کار',Provider=N'واحد آموزش' WHERE Id=@course AND CompanyId=@company;
INSERT leave.Balances(CompanyId,EmployeeId,PolicyId,Year,AccruedHours,UsedHours,UpdatedAtUtc) VALUES(@company,@employee,@policy,@balanceYear,160,24,SYSDATETIMEOFFSET());
DECLARE @n int=0;
WHILE @n < 24
BEGIN
  INSERT leave.Requests(CompanyId,EmployeeId,PolicyId,StartDate,EndDate,Hours,Status,RequestedAtUtc)
  VALUES(@company,@employee,@policy,DATEADD(day,@n,'2025-01-01'),DATEADD(day,@n,'2025-01-01'),8,'Approved',SYSDATETIMEOFFSET());
  INSERT training.Assignments(CompanyId,EmployeeId,CourseId,AssignedDate,DueDate,CompletedDate)
  VALUES(@company,@employee,@course,DATEADD(day,@n,'2025-01-01'),DATEADD(day,@n,'2025-02-01'),DATEADD(day,@n,'2025-01-02'));
  SET @n=@n+1;
END;
INSERT leave.Requests(CompanyId,EmployeeId,PolicyId,StartDate,EndDate,Hours,Status,RequestedAtUtc)
VALUES(@company,@employee,@policy,DATEFROMPARTS(@balanceYear,11,1),DATEFROMPARTS(@balanceYear,11,1),8,'Pending',SYSDATETIMEOFFSET());
INSERT training.Assignments(CompanyId,EmployeeId,CourseId,AssignedDate,DueDate)
VALUES(@company,@employee,@course,'2026-10-01','2026-10-31');

"@
        $fixtureFile = Join-Path $env:TEMP ('hrm-visual-fixture-' + $suffix + '.sql')
        try {
            [IO.File]::WriteAllText($fixtureFile,$visualFixture,[Text.UTF8Encoding]::new($true))
            $fixtureOutput = sqlcmd -S . -E -C -b -d $Database -i $fixtureFile 2>&1
            if ($LASTEXITCODE -ne 0) { throw "Visual fixture failed: $fixtureOutput" }
        } finally { Remove-Item -LiteralPath $fixtureFile -ErrorAction SilentlyContinue }
        & (Join-Path $PSScriptRoot 'CaptureProfile.ps1') -BaseUrl $baseUrl -Username $username -Password $password -EmployeeId $employeeId -OutputDirectory $ScreenshotsDirectory -SelfUsername $memberUsername -SelfPassword $memberPassword
    }

    $accounts=Invoke-WebRequest "$baseUrl/Settings/Users" -WebSession $webSession -UseBasicParsing
    $memberRow=[regex]::Matches($accounts.Content,'(?s)<tr data-user-id="[0-9]+">.*?</tr>') | Where-Object { $_.Value.Contains($memberUsername) } | Select-Object -First 1
    $memberId=[regex]::Match($memberRow.Value,'data-user-id="([0-9]+)"').Groups[1].Value
    $memberVersion=[Net.WebUtility]::HtmlDecode([regex]::Match($memberRow.Value,'name="version" value="([^"]+)"').Groups[1].Value)
    if(!$memberId -or !$memberVersion){throw 'Self-service account version was not available.'}
    $revoked=Invoke-WebRequest "$baseUrl/Settings/Users?handler=Update" -Method Post -WebSession $webSession -UseBasicParsing -Body @{
        id=$memberId; employeeId=$employeeId; canRead='false'; canWrite='false'; isActive='false'; version=$memberVersion; __RequestVerificationToken=(Get-FormToken $accounts.Content)
    }
    foreach($revokedPath in @('/MyInfo','/My/Leave', $profilePath, "$profilePath/Photo", "$profilePath/Documents")) {
        $revokedResponse=Invoke-WebRequest ($baseUrl+$revokedPath) -WebSession $memberSession -UseBasicParsing
        if($revokedResponse.BaseResponse.ResponseUri.AbsolutePath -ne '/Login'){throw 'Deactivated account retained self-service access.'}
    }
    Write-Output 'HTTPS login, Home, Settings, accounts, organization links, employee/photo, job change, Leave, Reports, Training, Documents: PASS'
}
finally {
    if ($process) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    $cleanup = "SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; SET QUOTED_IDENTIFIER ON; DECLARE @id int=(SELECT Id FROM organization.Companies WHERE Name='$companyName'); DELETE FROM training.Assignments WHERE CompanyId=@id; DELETE FROM training.Courses WHERE CompanyId=@id; DELETE FROM leave.Requests WHERE CompanyId=@id; DELETE FROM leave.Balances WHERE CompanyId=@id; DELETE FROM leave.Policies WHERE CompanyId=@id; DELETE FROM documents.EmployeeDocuments WHERE CompanyId=@id; DELETE FROM documents.DocumentAudits WHERE CompanyId=@id; DELETE FROM personnel.EmployeeAudits WHERE EmployeeId IN (SELECT Id FROM personnel.Employees WHERE CompanyId=@id); DELETE FROM personnel.EmployeePhotos WHERE CompanyId=@id; DELETE FROM personnel.EmployeeJobs WHERE CompanyId=@id; DELETE FROM personnel.Employees WHERE CompanyId=@id; DELETE FROM access.Audits WHERE CompanyId=@id; DELETE FROM access.Users WHERE CompanyId=@id; DELETE FROM access.RoleAudits WHERE CompanyId=@id; DELETE FROM access.Roles WHERE CompanyId=@id; DELETE FROM organization.OrganizationLinks WHERE CompanyId=@id; DELETE FROM organization.JobTitles WHERE CompanyId=@id; DELETE FROM organization.Departments WHERE CompanyId=@id; DELETE FROM organization.Companies WHERE Id=@id;"
    $cleanupOutput = sqlcmd -S . -E -C -b -d $Database -Q $cleanup 2>&1
    $cleanupExit = $LASTEXITCODE
    Remove-Item Env:ConnectionStrings__HrmsNext,Env:HRMS_BOOTSTRAP_USER,Env:HRMS_BOOTSTRAP_PASSWORD,Env:HRMS_BOOTSTRAP_COMPANY,Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
    if ($cleanupExit -ne 0) { throw "Smoke cleanup failed: $cleanupOutput" }
}
