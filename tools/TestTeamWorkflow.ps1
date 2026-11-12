param(
    [Parameter(Mandatory=$true)][string]$BaseUrl,
    [Parameter(Mandatory=$true)]$AdminSession,
    [Parameter(Mandatory=$true)][string]$CompanyName,
    [Parameter(Mandatory=$true)][int]$EmployeeId,
    [Parameter(Mandatory=$true)][int]$PeerId,
    [Parameter(Mandatory=$true)][int]$PolicyId,
    [Parameter(Mandatory=$true)][int]$DepartmentId,
    [string]$ScreenshotsDirectory
)
$ErrorActionPreference='Stop'
if(([Uri]$BaseUrl).Host -ne 'localhost' -or $CompanyName -notmatch '^Smoke_[a-f0-9]{32}$'){throw 'Trial smoke fixtures only.'}
function Token([string]$html){$tag=[regex]::Match($html,'<input[^>]*name="__RequestVerificationToken"[^>]*>');return [Net.WebUtility]::HtmlDecode([regex]::Match($tag.Value,'value="([^"]+)"').Groups[1].Value)}
function Field([string]$html,[string]$name){return [Net.WebUtility]::HtmlDecode([regex]::Match($html,('<input[^>]*name="'+[regex]::Escape($name)+'"[^>]*value="([^"]*)"')).Groups[1].Value)}
function Get([string]$path,$session=$AdminSession){Invoke-WebRequest ($BaseUrl+$path) -WebSession $session -UseBasicParsing}
function Post([string]$path,[hashtable]$body,$session=$AdminSession){Invoke-WebRequest ($BaseUrl+$path) -Method Post -Body $body -WebSession $session -UseBasicParsing}
function TrialSql([string]$statement){
    $sql="SET ANSI_NULLS ON;SET ANSI_PADDING ON;SET ANSI_WARNINGS ON;SET ARITHABORT ON;SET CONCAT_NULL_YIELDS_NULL ON;SET NUMERIC_ROUNDABORT OFF;SET QUOTED_IDENTIFIER ON; IF DB_NAME()<>N'HRMS_Next_Trial_20261108' THROW 50000,'Trial only',1; DECLARE @company int=(SELECT Id FROM organization.Companies WHERE Name='$CompanyName'); IF @company IS NULL THROW 50000,'Missing fixture',1; $statement"
    $result=sqlcmd -S . -E -C -b -d HRMS_Next_Trial_20261108 -Q $sql 2>&1
    if($LASTEXITCODE -ne 0){throw "Team fixture failed: $result"}
}
$unique=[Guid]::NewGuid().ToString('N').Substring(0,10)
$roles=Get '/Settings/Roles'
$roleCreated=Post '/Settings/Roles?handler=Save' @{Name=('Team smoke '+$unique);CanManageTeam='true';CanRead='false';CanWrite='false';Active='true';EditId='0';Version='';__RequestVerificationToken=(Token $roles.Content)}
$roleRow=[regex]::Matches($roleCreated.Content,'(?s)<tr data-role-id="[0-9]+">.*?</tr>')|Where-Object{$_.Value.Contains($unique)}|Select-Object -First 1
$roleId=[regex]::Match($roleRow.Value,'data-role-id="([0-9]+)"').Groups[1].Value
if(!$roleId){throw 'Role creation failed.'}
$create=Get '/Employees/Create'
$manager=Post '/Employees/Create' @{'Form.EmployeeNumber'=('M_'+$unique);'Form.FirstName'='Team';'Form.LastName'='Manager';'Form.DepartmentId'=$DepartmentId;'Form.HireDateJalali'='1403/01/01';__RequestVerificationToken=(Token $create.Content)}
$managerId=[regex]::Match($manager.BaseResponse.ResponseUri.AbsolutePath,'/Employees/([0-9]+)$').Groups[1].Value
if(!$managerId){throw 'Manager employee fixture failed.'}
$users=Get '/Settings/Users'
$managerUsername='smoke_manager_'+$unique;$managerPassword=[Guid]::NewGuid().ToString('N')+'A1!'
$invalid=Post '/Settings/Users?handler=Create' @{NewUsername=$managerUsername;NewPassword=$managerPassword;NewRoleId=$roleId;NewCanRead='false';NewCanWrite='false';__RequestVerificationToken=(Token $users.Content)}
if($invalid.Content -match ('<strong>'+$managerUsername+'</strong>')){throw 'Unlinked manager role assignment succeeded.'}
$created=Post '/Settings/Users?handler=Create' @{NewUsername=$managerUsername;NewPassword=$managerPassword;NewEmployeeId=$managerId;NewRoleId=$roleId;NewCanRead='false';NewCanWrite='false';__RequestVerificationToken=(Token $users.Content)}
if($created.Content -notmatch ('<strong>'+$managerUsername+'</strong>')){throw 'Linked manager account creation failed.'}
TrialSql "UPDATE personnel.EmployeeJobs SET ManagerEmployeeId=$managerId WHERE CompanyId=@company AND EmployeeId=$EmployeeId AND IsCurrent=1;"
$login=Invoke-WebRequest ($BaseUrl+'/Login') -SessionVariable teamSession -UseBasicParsing
$teamHome=Post '/Login' @{Username=$managerUsername;Password=$managerPassword;__RequestVerificationToken=(Token $login.Content)} $teamSession
if($teamHome.Content -notmatch 'href="/Team"' -or $teamHome.Content -match 'form class="searchbox"|home-celebrations|data-celebration='){throw 'Manager home navigation scope failed.'}
$team=Get '/Team' $teamSession
if($team.Content -notmatch "data-team-employee=`"$EmployeeId`"" -or $team.Content -match "data-team-employee=`"$PeerId`""){throw 'Team list scope failed.'}
$profile=Get "/Employees/$EmployeeId" $teamSession
if($profile.Content -notmatch 'profile-job-tab' -or $profile.Content -match 'data-private-personal|PRIVATE_PERSONAL_SMOKE|profile-pay-tab|profile-documents-tab|profile-leave-tab|profile-training-tab|data-pay-history|data-document-id|photo-modal|profile-neighbours'){throw 'Manager profile leaked private controls/data.'}
foreach($path in @("/Employees/$PeerId","/Employees/$EmployeeId/Edit","/Employees/$EmployeeId/Personal","/Employees/$EmployeeId/Documents",'/Employees','/Reports','/Leave','/Training','/Settings/Roles','/Settings/Users')){
    if((Get $path $teamSession).BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw "Manager accessed forbidden route $path"}
}
foreach($target in @($EmployeeId,$PeerId,$managerId)){
    $leave=Get '/Leave'
    $requested=Post '/Leave?handler=Request' @{EmployeeId=$target;PolicyId=$PolicyId;StartDateJalali='1403/05/01';EndDateJalali='1403/05/01';Reason='PRIVATE_TEAM_SMOKE';__RequestVerificationToken=(Token $leave.Content)}
}
$queue=Get '/Team/Leave' $teamSession
$queueRows=[regex]::Matches($queue.Content,'(?s)<tr data-team-request="[0-9]+">.*?</tr>')
if($queueRows.Count -ne 1 -or $queue.Content -match 'PRIVATE_TEAM_SMOKE'){throw 'Team queue scope/privacy failed.'}
$requestId=Field $queueRows[0].Value 'id';$version=Field $queueRows[0].Value 'version';$token=Token $queue.Content
$stale=Post '/Team/Leave?handler=Decide' @{id=$requestId;decision='Approve';version='AA==';__RequestVerificationToken=$token} $teamSession
if($stale.Content -notmatch 'data-team-request='){throw 'Stale manager request version was accepted.'}
try{Post '/Team/Leave?handler=Decide' @{id=$requestId;decision='Approve';version=$version} $teamSession|Out-Null;throw 'Missing CSRF was accepted.'}catch [System.Net.WebException]{if([int]$_.Exception.Response.StatusCode -ne 400){throw}}
TrialSql "UPDATE personnel.EmployeeJobs SET ManagerEmployeeId=NULL WHERE CompanyId=@company AND EmployeeId=$EmployeeId AND IsCurrent=1;"
if((Get '/Team/Leave' $teamSession).Content -match 'data-team-request='){throw 'Reassigned manager retained queue access.'}
$forged=Post '/Team/Leave?handler=Decide' @{id=$requestId;decision='Approve';version=$version;__RequestVerificationToken=$token} $teamSession
if($forged.BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw 'Reassigned manager approved request.'}
TrialSql "UPDATE personnel.EmployeeJobs SET ManagerEmployeeId=$managerId WHERE CompanyId=@company AND EmployeeId=$EmployeeId AND IsCurrent=1;"
if($ScreenshotsDirectory){
    & (Join-Path $PSScriptRoot 'CaptureTeam.ps1') -BaseUrl $BaseUrl -Username $managerUsername -Password $managerPassword -EmployeeId $EmployeeId -OutputDirectory $ScreenshotsDirectory
}
$approved=Post '/Team/Leave?handler=Decide' @{id=$requestId;decision='Approve';version=$version;__RequestVerificationToken=$token} $teamSession
if($approved.Content -match 'data-team-request='){throw 'Manager approval failed.'}
$replay=Post '/Team/Leave?handler=Decide' @{id=$requestId;decision='Approve';version=$version;__RequestVerificationToken=$token} $teamSession
if($replay.Content -notmatch 'validation-summary-errors'){throw 'Stale approval replay did not show an error.'}
$role=Get ("/Settings/Roles?edit="+$roleId);$roleVersion=Field $role.Content 'Version'
$badRole=Post '/Settings/Roles?handler=Save' @{Name=('Team smoke '+$unique);EditId=$roleId;Version='AA==';Active='false';__RequestVerificationToken=(Token $role.Content)}
if($badRole.Content -notmatch 'validation-summary-errors'){throw 'Stale role edit succeeded.'}
$archived=Post '/Settings/Roles?handler=Save' @{Name=('Team smoke '+$unique);EditId=$roleId;Version=$roleVersion;Active='false';CanManageTeam='true';__RequestVerificationToken=(Token $role.Content)}
if((Get '/Team' $teamSession).BaseResponse.ResponseUri.AbsolutePath -ne '/Login'){throw 'Archived role did not revoke existing cookie.'}
$login=Invoke-WebRequest ($BaseUrl+'/Login') -SessionVariable archivedSession -UseBasicParsing
$teamHome=Post '/Login' @{Username=$managerUsername;Password=$managerPassword;__RequestVerificationToken=(Token $login.Content)} $archivedSession
if($teamHome.Content -match 'href="/Team"'){throw 'Archived role retained manager navigation.'}
if((Get '/Team' $archivedSession).BaseResponse.ResponseUri.AbsolutePath -ne '/Denied'){throw 'Archived role retained team permission.'}
TrialSql "UPDATE personnel.EmployeeJobs SET ManagerEmployeeId=NULL WHERE CompanyId=@company AND EmployeeId=$EmployeeId AND IsCurrent=1;"
Write-Output 'Roles, role assignment, manager profile privacy, scoped team queue, stale/CSRF/reassignment decisions and role cookie revocation: PASS'