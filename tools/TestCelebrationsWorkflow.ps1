param(
    [Parameter(Mandatory=$true)][string]$BaseUrl,
    [Parameter(Mandatory=$true)]$AdminSession,
    [Parameter(Mandatory=$true)]$OwnerSession,
    [Parameter(Mandatory=$true)]$ReaderSession,
    [Parameter(Mandatory=$true)][string]$CompanyName
)
$ErrorActionPreference='Stop'
if(([Uri]$BaseUrl).Host -ne 'localhost' -or $CompanyName -notmatch '^Smoke_[a-f0-9]{32}$'){throw 'Local generated trial fixtures only.'}
$foreignName='CelebrationWeb_'+[Guid]::NewGuid().ToString('N')
$fixtureCode='CEL_'+[Guid]::NewGuid().ToString('N').Substring(0,12)
$calendar=[Globalization.PersianCalendar]::new()
$today=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow,'Iran Standard Time').Date
$month=$calendar.GetMonth($today);$day=$calendar.GetDayOfMonth($today)
$birth=$calendar.ToDateTime(1379,$month,$day,0,0,0,0).ToString('yyyy-MM-dd',[Globalization.CultureInfo]::InvariantCulture)
$anniversaryYear=$calendar.GetYear($today)-5
while($calendar.GetDaysInMonth($anniversaryYear,$month) -lt $day){$anniversaryYear--}
$anniversary=$calendar.ToDateTime($anniversaryYear,$month,$day,0,0,0,0).ToString('yyyy-MM-dd',[Globalization.CultureInfo]::InvariantCulture)
$otherHire=$today.AddMonths(-6).AddYears(-5).ToString('yyyy-MM-dd',[Globalization.CultureInfo]::InvariantCulture)
$todaySql=$today.ToString('yyyy-MM-dd',[Globalization.CultureInfo]::InvariantCulture)
$tomorrowSql=$today.AddDays(1).ToString('yyyy-MM-dd',[Globalization.CultureInfo]::InvariantCulture)
function TrialSql([string]$statement){
    $sql="SET ANSI_NULLS ON;SET ANSI_PADDING ON;SET ANSI_WARNINGS ON;SET ARITHABORT ON;SET CONCAT_NULL_YIELDS_NULL ON;SET NUMERIC_ROUNDABORT OFF;SET QUOTED_IDENTIFIER ON; IF DB_NAME()<>N'HRMS_Next_Trial_20261108' THROW 50000,'Trial only',1; DECLARE @company int=(SELECT Id FROM organization.Companies WHERE Name='$CompanyName'); IF @company IS NULL THROW 50000,'Missing fixture',1; $statement"
    $result=sqlcmd -S . -E -C -b -d HRMS_Next_Trial_20261108 -Q $sql 2>&1
    if($LASTEXITCODE -ne 0){throw "Celebrations trial fixture failed: $result"}
}
function Card([string]$html){
    $value=[regex]::Match($html,'(?s)<section class="card home-celebrations".*?</section>').Value
    if(!$value){throw 'Home celebrations card missing.'}
    return [Net.WebUtility]::HtmlDecode($value)
}
try {
    TrialSql @"
DECLARE @department int=(SELECT TOP(1) Id FROM organization.Departments WHERE CompanyId=@company ORDER BY Id);
IF @department IS NULL THROW 50000,'Missing fixture department',1;
INSERT personnel.Employees(CompanyId,DepartmentId,EmployeeNumber,FirstName,LastName,HireDate)
VALUES(@company,@department,'${fixtureCode}_B',N'پریسا',N'بهرامی','$otherHire');
DECLARE @birthday int=SCOPE_IDENTITY();
INSERT personnel.PersonalInfos(EmployeeId,CompanyId,DateOfBirth,MobilePhone,AddressLine1,EmergencyContactName)
VALUES(@birthday,@company,'$birth','PRIVATE_CELEBRATION_PHONE','PRIVATE_CELEBRATION_ADDRESS','PRIVATE_CELEBRATION_EMERGENCY');
INSERT personnel.Employees(CompanyId,DepartmentId,EmployeeNumber,FirstName,LastName,HireDate)
VALUES(@company,@department,'${fixtureCode}_A',N'نوید',N'احمدی','$anniversary');
INSERT personnel.Employees(CompanyId,DepartmentId,EmployeeNumber,FirstName,LastName,HireDate,TerminationDate)
VALUES(@company,@department,'${fixtureCode}_D','FORBIDDEN_CELEBRATION_DEPARTED','Hidden','$anniversary','$todaySql');
INSERT personnel.Employees(CompanyId,DepartmentId,EmployeeNumber,FirstName,LastName,HireDate)
VALUES(@company,@department,'${fixtureCode}_U','FORBIDDEN_CELEBRATION_FUTURE','Hidden','$tomorrowSql');
DECLARE @future int=SCOPE_IDENTITY();
INSERT personnel.PersonalInfos(EmployeeId,CompanyId,DateOfBirth) VALUES(@future,@company,'$birth');
INSERT organization.Companies(Name) VALUES('$foreignName');
DECLARE @foreign int=SCOPE_IDENTITY();
INSERT organization.Departments(CompanyId,Name) VALUES(@foreign,'Other');
DECLARE @foreignDepartment int=SCOPE_IDENTITY();
INSERT personnel.Employees(CompanyId,DepartmentId,EmployeeNumber,FirstName,LastName,HireDate)
VALUES(@foreign,@foreignDepartment,'${fixtureCode}_F','FORBIDDEN_CELEBRATION_FOREIGN','Hidden','$anniversary');
DECLARE @foreignEmployee int=SCOPE_IDENTITY();
INSERT personnel.PersonalInfos(EmployeeId,CompanyId,DateOfBirth) VALUES(@foreignEmployee,@foreign,'$birth');
"@
    foreach($session in @($AdminSession,$ReaderSession)){
        $response=Invoke-WebRequest ($BaseUrl+'/?CompanyId=2147483647&employeeId=2147483647') -WebSession $session -UseBasicParsing
        $card=Card $response.Content
        if($card -notmatch 'data-celebration="Birthday"' -or $card -notmatch 'data-celebration="Anniversary"' -or $card -notmatch 'پریسا بهرامی' -or $card -notmatch 'نوید احمدی'){throw 'Home celebration kinds or names missing.'}
        if($card -match 'FORBIDDEN_CELEBRATION|PRIVATE_CELEBRATION|1379/|۱۳۷۹/|DateOfBirth|MobilePhone'){throw 'Home celebration privacy/scope failed.'}
        if($card.Contains($birth)){throw 'Raw birth date leaked.'}
        if($card -notmatch 'href="/Employees/[1-9][0-9]*"'){throw 'Celebration profile link missing.'}
        if([regex]::Matches($card,'data-celebration=').Count -gt 6){throw 'Home celebration card exceeded6 events.'}
        $expectedDate=$calendar.GetYear($today).ToString('0000')+'/'+$month.ToString('00')+'/'+$day.ToString('00')
        $expectedDisplay=-join ($expectedDate.ToCharArray() | ForEach-Object {if($_ -ge [char]'0' -and $_ -le [char]'9'){[char](0x06f0+[int]$_-[int][char]'0')}else{$_}})
        if(!$card.Contains($expectedDisplay)){throw 'Celebrations used the wrong local Persian event date or digits.'}
    }
    $owner=Invoke-WebRequest ($BaseUrl+'/') -WebSession $OwnerSession -UseBasicParsing
    if($owner.Content -match 'home-celebrations|data-celebration=|PRIVATE_CELEBRATION'){throw 'Owner-only Home received company celebrations.'}
    Write-Output 'Home birthdays/anniversaries, Persian event date, safe profile links, tenant/current-employment scope and private birth-year exclusion: PASS'
}
finally {
    TrialSql "DECLARE @foreign int=(SELECT Id FROM organization.Companies WHERE Name='$foreignName'); DELETE personnel.PersonalInfos WHERE CompanyId=@foreign; DELETE personnel.Employees WHERE CompanyId=@foreign; DELETE organization.Departments WHERE CompanyId=@foreign; DELETE organization.Companies WHERE Id=@foreign;"
}
