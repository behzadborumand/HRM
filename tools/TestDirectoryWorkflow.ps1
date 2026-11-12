param(
    [Parameter(Mandatory=$true)][string]$BaseUrl,
    [Parameter(Mandatory=$true)]$AdminSession,
    [Parameter(Mandatory=$true)]$ReaderSession,
    [Parameter(Mandatory=$true)][string]$CompanyName
)
$ErrorActionPreference='Stop'
if(([Uri]$BaseUrl).Host -ne 'localhost' -or $CompanyName -notmatch '^Smoke_[a-f0-9]{32}$'){throw 'Generated local trial fixtures only.'}
$foreignName='DirectoryWeb_'+[Guid]::NewGuid().ToString('N')
function TrialSql([string]$statement){
    $sql="SET NOCOUNT ON;SET ANSI_NULLS ON;SET ANSI_PADDING ON;SET ANSI_WARNINGS ON;SET ARITHABORT ON;SET CONCAT_NULL_YIELDS_NULL ON;SET NUMERIC_ROUNDABORT OFF;SET QUOTED_IDENTIFIER ON;IF DB_NAME()<>N'HRMS_Next_Trial_20261108' THROW 50000,'Trial only',1;DECLARE @company int=(SELECT Id FROM organization.Companies WHERE Name='$CompanyName');IF @company IS NULL THROW 50000,'Missing fixture',1;$statement"
    $result=sqlcmd -S . -E -C -b -d HRMS_Next_Trial_20261108 -h -1 -W -Q $sql 2>&1
    if($LASTEXITCODE -ne 0){throw "Directory fixture failed: $result"}
    return ($result -join [Environment]::NewLine).Trim()
}
function Rows([string]$html){$body=[regex]::Match($html,'(?s)<tbody>(.*?)</tbody>').Groups[1].Value;return [regex]::Matches($body,'(?s)<tr(?:\s[^>]*)?>.*?</tr>')}
try {
    $department=TrialSql @"
INSERT organization.Departments(CompanyId,Name) VALUES(@company,N'واحد آزمایشی فهرست');
DECLARE @department int=SCOPE_IDENTITY(),@n int=0,@person int,@manager int=(SELECT TOP(1) Id FROM personnel.Employees WHERE CompanyId=@company ORDER BY Id);
WHILE @n<25
BEGIN
 INSERT personnel.Employees(CompanyId,DepartmentId,EmployeeNumber,FirstName,LastName,Email,HireDate,TerminationDate)
 VALUES(@company,@department,'DIRECTORY_UI_'+RIGHT('000'+CONVERT(varchar(3),@n),3),N'همکار','DirectoryUI'+RIGHT('000'+CONVERT(varchar(3),@n),3),'directory'+CONVERT(varchar(3),@n)+'@example.test','2020-01-01',CASE WHEN @n=24 THEN '2024-01-01' ELSE NULL END);
 SET @person=SCOPE_IDENTITY();
 IF @n=0
 BEGIN
  INSERT personnel.EmployeeJobs(CompanyId,EmployeeId,DepartmentId,ManagerEmployeeId,JobTitle,EmploymentType,StartDate,IsCurrent,PayRate)
  VALUES(@company,@person,@department,@manager,N'کارشناس منابع انسانی','FullTime','2020-01-01',1,987654321.99);
  DECLARE @png varbinary(max)=0x89504E470D0A1A0A0000000D4948445200000001000000010804000000B51C0C020000000B4944415478DA63FCFF1F0003030200EFA3E25F0000000049454E44AE426082;
  INSERT personnel.EmployeePhotos(EmployeeId,CompanyId,Bytes,ContentType,Sha256,UploadedAtUtc)
  VALUES(@person,@company,@png,'image/png',CONVERT(varchar(64),HASHBYTES('SHA2_256',@png),2),SYSDATETIMEOFFSET());
 END;
 SET @n=@n+1;
END;
INSERT organization.Companies(Name) VALUES('$foreignName');
DECLARE @foreign int=SCOPE_IDENTITY();
INSERT organization.Departments(CompanyId,Name) VALUES(@foreign,'Other');
DECLARE @other int=SCOPE_IDENTITY();
INSERT personnel.Employees(CompanyId,DepartmentId,EmployeeNumber,FirstName,LastName,HireDate) VALUES(@foreign,@other,'DIRECTORY_UI_FOREIGN','FORBIDDEN_DIRECTORY','Hidden','2020-01-01');
SELECT @department;
"@
    if($department -notmatch '^[1-9][0-9]*$'){throw 'Directory department ID missing.'}
    $query="/Employees?query=DIRECTORY_UI_&departmentId=$department&status=Active"
    foreach($session in @($AdminSession,$ReaderSession)){
        $first=Invoke-WebRequest ($BaseUrl+$query+'&pageNumber=1') -WebSession $session -UseBasicParsing
        $second=Invoke-WebRequest ($BaseUrl+$query+'&pageNumber=2') -WebSession $session -UseBasicParsing
        if((Rows $first.Content).Count -ne 20 -or (Rows $second.Content).Count -ne 4){throw 'Employee pageNumber2 must retain the filter and contain4 rows after20 on page1.'}
        if($second.Content -notmatch 'DIRECTORY_UI_020' -or $second.Content -match 'DIRECTORY_UI_000|DIRECTORY_UI_024|FORBIDDEN_DIRECTORY'){throw 'Directory page scope or ordering failed.'}
        $firstHtml=[Net.WebUtility]::HtmlDecode($first.Content)
        if($firstHtml -notmatch 'کارشناس منابع انسانی' -or $firstHtml -notmatch 'data-directory-photo' -or $firstHtml -notmatch '/Photo\?v=' -or $firstHtml -notmatch 'pageNumber=2'){throw 'Directory current title, portrait or usable pagination missing.'}
        if($firstHtml -match '987654321|PayRate|PRIVATE_PERSONAL_SMOKE|PRIVATE_ADDRESS_SMOKE'){throw 'Private fields leaked to directory.'}
        $photoTag=[regex]::Match($firstHtml,'<img[^>]*data-directory-photo[^>]*>').Value
        $photoPath=[regex]::Match($photoTag,'src="([^"]+)"').Groups[1].Value
        if(!$photoPath){throw 'Portrait source missing.'}
        $photo=Invoke-WebRequest ($BaseUrl+$photoPath) -WebSession $session -UseBasicParsing
        if($photo.StatusCode -ne 200 -or $photo.Headers['Content-Type'] -notmatch 'image/png'){throw 'Directory portrait did not load.'}
        $terminated=Invoke-WebRequest ($BaseUrl+"/Employees?query=DIRECTORY_UI_&departmentId=$department&status=Terminated") -WebSession $session -UseBasicParsing
        if((Rows $terminated.Content).Count -ne 1 -or $terminated.Content -notmatch 'DIRECTORY_UI_024'){throw 'Terminated directory filter failed.'}
        $empty=Invoke-WebRequest ($BaseUrl+'/Employees?query=DIRECTORY_NO_MATCH_'+[Guid]::NewGuid().ToString('N')) -WebSession $session -UseBasicParsing
        if($empty.Content -notmatch 'colspan="9"'){throw 'Directory empty state does not span its real columns.'}
    }
    Write-Output 'Directory scoped current title/manager/portrait,20+4 paging, filters, empty state and private-data exclusion: PASS'
}
finally {
    TrialSql "DECLARE @foreign int=(SELECT Id FROM organization.Companies WHERE Name='$foreignName');DELETE personnel.Employees WHERE CompanyId=@foreign;DELETE organization.Departments WHERE CompanyId=@foreign;DELETE organization.Companies WHERE Id=@foreign;"|Out-Null
}
