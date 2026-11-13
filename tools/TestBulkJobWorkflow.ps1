param([string]$BaseUrl,$AdminSession,$ReaderSession,[string]$CompanyName)
$ErrorActionPreference='Stop'
if(([Uri]$BaseUrl).Host -ne 'localhost' -or $CompanyName -notmatch '^Smoke_[a-f0-9]{32}$'){throw 'Generated local trial fixtures only.'}
function Token([string]$html){$tag=[regex]::Match($html,'<input[^>]*name="__RequestVerificationToken"[^>]*>');return [Net.WebUtility]::HtmlDecode([regex]::Match($tag.Value,'value="([^"]+)"').Groups[1].Value)}
function TrialSql([string]$statement){
    $sql="SET NOCOUNT ON;SET ANSI_NULLS ON;SET ANSI_PADDING ON;SET ANSI_WARNINGS ON;SET ARITHABORT ON;SET CONCAT_NULL_YIELDS_NULL ON;SET NUMERIC_ROUNDABORT OFF;SET QUOTED_IDENTIFIER ON;IF DB_NAME()<>N'HRMS_Next_Trial_20261108' THROW 50000,'Trial only',1;DECLARE @company int=(SELECT Id FROM organization.Companies WHERE Name='$CompanyName');IF @company IS NULL THROW 50000,'Missing fixture',1;$statement"
    $result=sqlcmd -S . -E -C -b -d HRMS_Next_Trial_20261108 -h -1 -W -Q $sql 2>&1
    if($LASTEXITCODE -ne 0){throw "Bulk fixture failed: $result"};return ($result -join [Environment]::NewLine).Trim()
}
function FormBody([string]$html){
    $body=@{__RequestVerificationToken=(Token $html);'Form.DepartmentId'='';'Form.JobTitle'='';'Form.ManagerEmployeeId'='';'Form.EmploymentType'='PartTime';'Form.EffectiveDateJalali'='1402/01/01'}
    foreach($tag in [regex]::Matches($html,'<input[^>]*name="Form.Selected\[[0-9]+\]\.(?:EmployeeId|Version)"[^>]*>')){
        $name=[regex]::Match($tag.Value,'name="([^"]+)"').Groups[1].Value;$value=[Net.WebUtility]::HtmlDecode([regex]::Match($tag.Value,'value="([^"]+)"').Groups[1].Value);$body[$name]=$value
    }
    return $body
}
function Post($body,$session){return Invoke-WebRequest "$BaseUrl/Employees/BulkEdit" -Method Post -Body $body -WebSession $session -UseBasicParsing}
function ExpectCode([scriptblock]$action,[int]$code){try{& $action|Out-Null;throw "Expected HTTP$code"}catch{if(!$_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $code){throw}}}
$list=Invoke-WebRequest "$BaseUrl/Employees?status=Active" -WebSession $AdminSession -UseBasicParsing
if($list.Content -notmatch 'data-select-all' -or $list.Content -notmatch 'data-bulk-submit'){throw 'Writer directory is missing current-page bulk selection.'}
$read=Invoke-WebRequest "$BaseUrl/Employees" -WebSession $ReaderSession -UseBasicParsing
if($read.Content -match 'data-select-all|data-bulk-submit|data-select-employee'){throw 'Reader gained bulk selection.'}
$ids=(TrialSql @"
DECLARE @department int=(SELECT TOP(1) Id FROM organization.Departments WHERE CompanyId=@company ORDER BY Id),@n int=0,@id int;
WHILE @n<2
BEGIN
 INSERT personnel.Employees(CompanyId,DepartmentId,EmployeeNumber,FirstName,LastName,HireDate) VALUES(@company,@department,'BULK_UI_'+CONVERT(varchar(1),@n),N'همکار گروهی',N'آزمایشی','2020-01-01');
 SET @id=SCOPE_IDENTITY();
 INSERT personnel.EmployeeJobs(CompanyId,EmployeeId,DepartmentId,JobTitle,EmploymentType,StartDate,IsCurrent,PayType,PayRate) VALUES(@company,@id,@department,N'عنوان اولیه','FullTime','2021-01-01',1,'Salary',876543210.12);
 SET @n=@n+1;
END;
SELECT Id FROM personnel.Employees WHERE CompanyId=@company AND EmployeeNumber LIKE 'BULK_UI[_]%' ORDER BY Id;
"@) -split '\s+'
if($ids.Count -ne 2){throw 'Bulk fixture count invalid.'}
$path='/Employees/BulkEdit?ids='+$ids[0]+'&ids='+$ids[1]
$denied=Invoke-WebRequest ($BaseUrl+$path) -WebSession $ReaderSession -UseBasicParsing
if($denied.BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw 'Reader accessed bulk editor.'}
$users=Invoke-WebRequest "$BaseUrl/Settings/Users" -WebSession $AdminSession -UseBasicParsing
$username='smoke_bulk_'+[Guid]::NewGuid().ToString('N').Substring(0,12);$password=[Guid]::NewGuid().ToString('N')+'A1!'
Invoke-WebRequest "$BaseUrl/Settings/Users?handler=Create" -Method Post -WebSession $AdminSession -UseBasicParsing -Body @{NewUsername=$username;NewPassword=$password;NewEmployeeId='';NewCanRead='false';NewCanWrite='true';__RequestVerificationToken=(Token $users.Content)}|Out-Null
$login=Invoke-WebRequest "$BaseUrl/Login" -SessionVariable writerSession -UseBasicParsing
Invoke-WebRequest "$BaseUrl/Login" -Method Post -WebSession $writerSession -UseBasicParsing -Body @{Username=$username;Password=$password;__RequestVerificationToken=(Token $login.Content)}|Out-Null
$page=Invoke-WebRequest ($BaseUrl+$path) -WebSession $writerSession -UseBasicParsing
if([regex]::Matches($page.Content,'data-bulk-employee=').Count -ne 2 -or $page.Content -match '876543210|PayRate|PRIVATE_PERSONAL'){throw 'Bulk snapshot count/privacy invalid.'}
$body=FormBody $page.Content
$invalid=FormBody $page.Content;$invalid['Form.EffectiveDateJalali']='bad-date'
$response=Post $invalid $writerSession
if($response.Content -notmatch 'validation-summary-errors' -or $response.Content -notmatch 'value="bad-date"' -or $response.Content -notmatch '<option(?=[^>]*value="PartTime")(?=[^>]*selected="selected")[^>]*>'){[IO.File]::WriteAllText((Join-Path $env:TEMP 'HRM-Bulk-invalid.html'),$response.Content,[Text.UTF8Encoding]::new($false));throw 'Bulk invalid input not retained; diagnostic HTML saved in HRM-Bulk-invalid.html.'}
$body['Form.PayRate']='0';$body['Form.TerminationDate']='2020-01-01';$body['Form.CompanyId']='999999'
$response=Post $body $writerSession
if($response.Content -notmatch 'data-bulk-result'){throw 'Writer bulk save failed.'}
$check=TrialSql "SELECT CONCAT(COUNT(*),'|',SUM(CASE WHEN IsCurrent=1 AND EmploymentType='PartTime' AND PayRate=876543210.12 AND PayType='Salary' THEN 1 ELSE 0 END),'|',SUM(CASE WHEN IsCurrent=0 AND EndDate='2023-03-20' THEN 1 ELSE 0 END)) FROM personnel.EmployeeJobs WHERE CompanyId=@company AND EmployeeId IN ($($ids -join ','));"
if($check -ne '4|2|2'){throw "Bulk history/pay mismatch: $check"}
$stale=Post $body $writerSession
if($stale.Content -notmatch 'validation-summary-errors'){throw 'Stale bulk replay accepted.'}
$fresh=FormBody $response.Content;$noOp=Post $fresh $writerSession
if($noOp.Content -notmatch 'data-bulk-result'){throw 'Bulk no-op did not report success.'}
$check=TrialSql "SELECT CONCAT((SELECT COUNT(*) FROM personnel.EmployeeJobs WHERE CompanyId=@company AND EmployeeId IN ($($ids -join ','))),'|',(SELECT COUNT(*) FROM personnel.EmployeeAudits WHERE EmployeeId IN ($($ids -join ',')) AND Action='BulkJobChange'));"
if($check -ne '4|2'){throw 'Bulk no-op/replay duplicated history or audit.'}
$noCsrf=FormBody $response.Content;$noCsrf.Remove('__RequestVerificationToken');ExpectCode {Post $noCsrf $writerSession} 400
$readerPost=FormBody $response.Content;$readerPost['__RequestVerificationToken']=Token $read.Content
$r=Post $readerPost $ReaderSession
if($r.BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw 'Reader submitted bulk edit.'}
ExpectCode {Invoke-WebRequest "$BaseUrl/Employees/BulkEdit?ids=$($ids[0])&ids=$($ids[0])" -WebSession $writerSession -UseBasicParsing} 400
ExpectCode {Invoke-WebRequest "$BaseUrl/Employees/BulkEdit?ids=$($ids[0])&ids=2147483647" -WebSession $writerSession -UseBasicParsing} 404
$forged=FormBody $response.Content;$forged['Form.Selected[1].EmployeeId']='2147483647';ExpectCode {Post $forged $writerSession} 404
Write-Output 'Bulk writer change, private pay preservation, dated/audited history, no-op, stale/invalid/forged selection, reader denial and CSRF: PASS'