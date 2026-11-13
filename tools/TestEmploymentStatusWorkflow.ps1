param([string]$BaseUrl,$AdminSession,$ReaderSession,$OwnerSession,[string]$CompanyName,[int]$EmployeeId)
$ErrorActionPreference='Stop'
if(([Uri]$BaseUrl).Host -ne 'localhost' -or $CompanyName -notmatch '^Smoke_[a-f0-9]{32}$'){throw 'Generated local trial fixtures only.'}
function Token([string]$html){$tag=[regex]::Match($html,'<input[^>]*name="__RequestVerificationToken"[^>]*>');return [Net.WebUtility]::HtmlDecode([regex]::Match($tag.Value,'value="([^"]+)"').Groups[1].Value)}
function Value([string]$html,[string]$name){$tag=[regex]::Match($html,'<input[^>]*name="'+[regex]::Escape($name)+'"[^>]*>');return [Net.WebUtility]::HtmlDecode([regex]::Match($tag.Value,'value="([^"]*)"').Groups[1].Value)}
function TrialSql([string]$statement){
    $sql="SET NOCOUNT ON;SET ANSI_NULLS ON;SET ANSI_PADDING ON;SET ANSI_WARNINGS ON;SET ARITHABORT ON;SET CONCAT_NULL_YIELDS_NULL ON;SET NUMERIC_ROUNDABORT OFF;SET QUOTED_IDENTIFIER ON;IF DB_NAME()<>N'HRMS_Next_Trial_20261108' THROW 50000,'Trial only',1;DECLARE @company int=(SELECT Id FROM organization.Companies WHERE Name='$CompanyName');IF @company IS NULL THROW 50000,'Missing fixture',1;$statement"
    $result=sqlcmd -S . -E -C -b -d HRMS_Next_Trial_20261108 -h -1 -W -Q $sql 2>&1
    if($LASTEXITCODE -ne 0){throw "Status fixture failed: $result"};return ($result -join [Environment]::NewLine).Trim()
}
function ExpectCode([scriptblock]$action,[int]$code){try{& $action|Out-Null;throw "Expected HTTP$code"}catch{if(!$_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $code){throw}}}
function StatusBody([string]$html,[string]$status='OnLeave') {return @{__RequestVerificationToken=(Token $html);'StatusForm.Version'=(Value $html 'StatusForm.Version');'StatusForm.Status'=$status;'StatusForm.EffectiveDateJalali'='1404/01/01'}}
$edit=Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Edit" -WebSession $AdminSession -UseBasicParsing
if($edit.Content -notmatch 'id="employment-status-change"' -or $edit.Content -notmatch 'name="StatusForm.Status"'){throw 'Individual employment status controls are absent.'}
$before=TrialSql "SELECT CONCAT(COUNT(*),'|',SUM(ISNULL(PayRate,0))) FROM personnel.EmployeeJobs WHERE CompanyId=@company AND EmployeeId=$EmployeeId;"
$bad=StatusBody $edit.Content 'Terminated'
$r=Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Edit?handler=Status" -Method Post -WebSession $AdminSession -Body $bad -UseBasicParsing
if($r.Content -notmatch 'validation-summary-errors'){throw 'Forged termination status accepted.'}
$body=StatusBody $edit.Content;$body['Form.PayRate']='0';$body['Form.FirstName']='FORGED_STATUS_NAME';$body['Form.DepartmentId']='999999'
$r=Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Edit?handler=Status" -Method Post -WebSession $AdminSession -Body $body -UseBasicParsing
if($r.BaseResponse.ResponseUri.AbsolutePath -ne "/Employees/$EmployeeId" -or $r.Content -notmatch 'data-employment-status="OnLeave"'){[IO.File]::WriteAllText((Join-Path $env:TEMP 'HRM-Status-Failure.html'),$r.Content,[Text.UTF8Encoding]::new($false));throw ('Individual status/history save failed; path='+$r.BaseResponse.ResponseUri.AbsolutePath+'; diagnostic HTML saved.')}
$state=TrialSql "SELECT CONCAT(CONVERT(int,IsOnLeave),'|',FirstName) FROM personnel.Employees WHERE Id=$EmployeeId AND CompanyId=@company;"
if(!$state.StartsWith('1|') -or $state.Contains('FORGED_STATUS')){throw 'Status binding changed identity or failed state.'}
$after=TrialSql "SELECT CONCAT(COUNT(*),'|',SUM(ISNULL(PayRate,0))) FROM personnel.EmployeeJobs WHERE CompanyId=@company AND EmployeeId=$EmployeeId;"
if($before -ne $after){throw 'Status-only save modified job/pay history.'}
foreach($session in @($ReaderSession,$OwnerSession)){
    $profile=Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId" -WebSession $session -UseBasicParsing
    if($profile.Content -notmatch 'data-employment-status="OnLeave"'){throw 'Authorized profile lost safe status history.'}
    if($session -eq $ReaderSession -and $profile.Content -match 'data-pay-history|PRIVATE_PERSONAL'){throw 'Reader status history leaked private data.'}
    $deniedBody=StatusBody $edit.Content 'Active';$deniedBody['__RequestVerificationToken']=Token $profile.Content
    $denied=Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Edit?handler=Status" -Method Post -WebSession $session -Body $deniedBody -UseBasicParsing
    if($denied.BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw 'Reader/owner wrote employment status.'}
}
$stale=StatusBody $edit.Content 'Active'
$r=Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Edit?handler=Status" -Method Post -WebSession $AdminSession -Body $stale -UseBasicParsing
if($r.Content -notmatch 'validation-summary-errors'){throw 'Stale status replay accepted.'}
$fresh=Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Edit" -WebSession $AdminSession -UseBasicParsing
$noCsrf=StatusBody $fresh.Content;$noCsrf.Remove('__RequestVerificationToken')
ExpectCode {Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Edit?handler=Status" -Method Post -WebSession $AdminSession -Body $noCsrf -UseBasicParsing} 400
$invalid=StatusBody $fresh.Content 'Active';$invalid['StatusForm.EffectiveDateJalali']='invalid-status-date'
$r=Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Edit?handler=Status" -Method Post -WebSession $AdminSession -Body $invalid -UseBasicParsing
if($r.Content -notmatch 'validation-summary-errors' -or $r.Content -notmatch 'value="invalid-status-date"' -or $r.Content -notmatch 'employee-sidebar'){throw 'Status invalid values/profile frame not retained.'}
if([regex]::Matches($r.Content,'class="text-danger validation-summary-errors"').Count -ne 1){throw 'Status validation errors are duplicated under unrelated forms.'}
$backdated=StatusBody $fresh.Content 'Active';$backdated['StatusForm.EffectiveDateJalali']='1403/12/29'
$r=Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Edit?handler=Status" -Method Post -WebSession $AdminSession -Body $backdated -UseBasicParsing
if($r.Content -notmatch 'validation-summary-errors'){throw 'Backdated status transition accepted.'}
# Same state no-op, then real same-day return; exactly two dated history records.
Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Edit?handler=Status" -Method Post -WebSession $AdminSession -Body (StatusBody $fresh.Content) -UseBasicParsing|Out-Null
Invoke-WebRequest "$BaseUrl/Employees/$EmployeeId/Edit?handler=Status" -Method Post -WebSession $AdminSession -Body (StatusBody $fresh.Content 'Active') -UseBasicParsing|Out-Null
if((TrialSql "SELECT COUNT(*) FROM personnel.EmploymentStatusChanges WHERE CompanyId=@company AND EmployeeId=$EmployeeId;") -ne '2'){throw 'Status no-op/history mismatch.'}
$ids=(TrialSql "SELECT Id FROM personnel.Employees WHERE CompanyId=@company AND EmployeeNumber LIKE 'BULK_UI[_]%' ORDER BY Id;") -split '\s+'
$path='/Employees/BulkEdit?ids='+$ids[0]+'&ids='+$ids[1]
function BulkBody([string]$html){
    $data=@{__RequestVerificationToken=(Token $html);'Form.DepartmentId'='';'Form.JobTitle'='';'Form.ManagerEmployeeId'='';'Form.EmploymentType'='';'Form.EmploymentStatus'='OnLeave';'Form.EffectiveDateJalali'='1404/01/01'}
    for($i=0;$i -lt 2;$i++){foreach($field in @('EmployeeId','Version')){$key="Form.Selected[$i].$field";$data[$key]=Value $html $key}}
    return $data
}
$page=Invoke-WebRequest ($BaseUrl+$path) -WebSession $AdminSession -UseBasicParsing
if($page.Content -notmatch 'name="Form.EmploymentStatus"' -or $page.Content -notmatch '<th scope="col">نوع استخدام</th>'){throw 'Bulk status/type comparison controls missing.'}
$bulk=BulkBody $page.Content
$r=Invoke-WebRequest "$BaseUrl/Employees/BulkEdit" -Method Post -WebSession $AdminSession -Body $bulk -UseBasicParsing
if($r.Content -notmatch 'data-bulk-result'){throw 'Status-only bulk change failed.'}
$staleBulk=Invoke-WebRequest "$BaseUrl/Employees/BulkEdit" -Method Post -WebSession $AdminSession -Body $bulk -UseBasicParsing
if($staleBulk.Content -notmatch 'validation-summary-errors'){throw 'Stale bulk status replay accepted.'}
$foreign=TrialSql 'SELECT TOP(1) Id FROM personnel.Employees WHERE CompanyId<>@company ORDER BY Id;'
$forged=BulkBody $r.Content;$forged['Form.Selected[1].EmployeeId']=$foreign
ExpectCode {Invoke-WebRequest "$BaseUrl/Employees/BulkEdit" -Method Post -WebSession $AdminSession -Body $forged -UseBasicParsing} 404
$invalidBulk=BulkBody $r.Content;$invalidBulk['Form.EmploymentStatus']='Active';$invalidBulk['Form.DepartmentId']='2147483647'
$failed=Invoke-WebRequest "$BaseUrl/Employees/BulkEdit" -Method Post -WebSession $AdminSession -Body $invalidBulk -UseBasicParsing
if($failed.Content -notmatch 'validation-summary-errors'){throw 'Invalid combined status/job group accepted.'}
Invoke-WebRequest "$BaseUrl/Employees/BulkEdit" -Method Post -WebSession $AdminSession -Body (BulkBody $r.Content) -UseBasicParsing|Out-Null
$check=TrialSql "SELECT CONCAT((SELECT COUNT(*) FROM personnel.Employees WHERE CompanyId=@company AND Id IN ($($ids -join ',')) AND IsOnLeave=1),'|',(SELECT COUNT(*) FROM personnel.EmploymentStatusChanges WHERE CompanyId=@company AND EmployeeId IN ($($ids -join ','))),'|',(SELECT COUNT(*) FROM personnel.EmployeeJobs WHERE CompanyId=@company AND EmployeeId IN ($($ids -join ','))));"
if($check -ne '2|2|4'){throw "Bulk status/no-op/rollback/job preservation mismatch: $check"}
$list=Invoke-WebRequest "$BaseUrl/Employees?query=BULK_UI_&status=OnLeave" -WebSession $ReaderSession -UseBasicParsing
if([regex]::Matches($list.Content,'data-employee-id=').Count -ne 2){throw 'Directory OnLeave filter failed.'}
$active=Invoke-WebRequest "$BaseUrl/Employees?query=BULK_UI_&status=Active" -WebSession $ReaderSession -UseBasicParsing
if([regex]::Matches($active.Content,'data-employee-id=').Count -ne 0){throw 'Active directory includes OnLeave status.'}
Write-Output 'Employment status single/bulk history, status-only pay preservation, no-op/date/version/CSRF/tenant/grant boundaries, invalid input and directory filters: PASS'