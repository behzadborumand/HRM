param([string]$Database='HRMS_Next_Trial_20261108')
$ErrorActionPreference='Stop'
if($Database -ne 'HRMS_Next_Trial_20261108'){throw 'Trial only.'}
$import=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../database/import-employment-status.sql')).Path
function RunImport {
    $output=sqlcmd -S . -E -C -b -d $Database -i $import 2>&1
    if($LASTEXITCODE -ne 0){throw "Status import failed: $output"}
    if(($output -join ' ') -notmatch 'source=12 mapped=12 compared=12 onleave=1'){throw "Expected reconciled12/12/12/1; actual: $output"}
    Write-Output $output
}
RunImport
RunImport
function Reject([string]$mutation,[int]$number){
    $caseFile=Join-Path $env:TEMP ('hrm-status-import-case-'+[Guid]::NewGuid().ToString('N')+'.sql')
    $sql="SET XACT_ABORT ON;SET QUOTED_IDENTIFIER ON;SET ANSI_NULLS ON;SET ANSI_PADDING ON;SET ANSI_WARNINGS ON;SET ARITHABORT ON;SET CONCAT_NULL_YIELDS_NULL ON;SET NUMERIC_ROUNDABORT OFF;IF DB_NAME()<>N'HRMS_Next_Trial_20261108' THROW 51000,'Trial only',1;BEGIN TRANSACTION; "+$mutation+[Environment]::NewLine+':r "'+$import+'"'+[Environment]::NewLine+'IF @@TRANCOUNT>0 ROLLBACK;'
    try {
        [IO.File]::WriteAllText($caseFile,$sql,[Text.UTF8Encoding]::new($true))
        $output=sqlcmd -S . -E -C -b -d $Database -i $caseFile 2>&1
        if($LASTEXITCODE -eq 0 -or ($output -join ' ') -notmatch ("Msg "+$number+",")){throw "Expected status-import rejection $number; result: $output"}
    }finally{Remove-Item -LiteralPath $caseFile -ErrorAction SilentlyContinue}
}
Reject "UPDATE personnel.Employees SET IsOnLeave=1-IsOnLeave WHERE Id=(SELECT MIN(EmployeeId) FROM migration.EmploymentStatusMap);" 51008
Reject "UPDATE migration.EmploymentStatusMap SET SnapshotHash=0x00 WHERE EmployeeId=(SELECT MIN(EmployeeId) FROM migration.EmploymentStatusMap);" 51007
Reject "DELETE migration.EmploymentStatusMap WHERE EmployeeId=(SELECT MIN(EmployeeId) FROM migration.EmploymentStatusMap);" 51009
Reject "DECLARE @id int=(SELECT MIN(EmployeeId) FROM migration.EmploymentStatusMap WHERE SourceStatus=N'Active');DECLARE @company int=(SELECT CompanyId FROM personnel.Employees WHERE Id=@id);INSERT personnel.EmploymentStatusChanges(CompanyId,EmployeeId,EffectiveDate,FromStatus,ToStatus,ActorUserId,AtUtc) VALUES(@company,@id,'2025-01-01',N'Active',N'OnLeave',1,SYSDATETIMEOFFSET()),(@company,@id,'2025-01-01',N'OnLeave',N'Active',1,SYSDATETIMEOFFSET());" 51008
Reject "DECLARE @id int=(SELECT MIN(EmployeeId) FROM migration.EmploymentStatusMap);DELETE migration.EmploymentStatusMap WHERE EmployeeId=@id;DELETE personnel.EmploymentStatusChanges WHERE EmployeeId=@id;UPDATE migration.EmploymentStatusMap SET SnapshotHash=0x00 WHERE EmployeeId=(SELECT MIN(EmployeeId) FROM migration.EmploymentStatusMap);" 51007
$wrongDb=sqlcmd -S . -E -C -b -d tempdb -i $import 2>&1
if($LASTEXITCODE -eq 0 -or ($wrongDb -join ' ') -notmatch 'Msg 51000,'){throw 'Wrong-database guard failed.'}
RunImport
Write-Output 'Employment status import target/provenance/unmapped/cycle conflicts, whole-group rollback and wrong-database guard: PASS'