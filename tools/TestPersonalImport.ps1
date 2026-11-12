param([string]$Database='HRMS_Next_Trial_20261108')
$ErrorActionPreference='Stop'
if($Database -ne 'HRMS_Next_Trial_20261108'){throw 'Trial only.'}
$import=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../database/import-personal-info.sql')).Path
function Reject([string]$mutation,[int]$number){
    $caseFile=Join-Path $env:TEMP ('hrm-personal-import-case-'+[Guid]::NewGuid().ToString('N')+'.sql')
    $sql="SET XACT_ABORT ON;SET QUOTED_IDENTIFIER ON;SET ANSI_NULLS ON;SET ANSI_PADDING ON;SET ANSI_WARNINGS ON;SET ARITHABORT ON;SET CONCAT_NULL_YIELDS_NULL ON;SET NUMERIC_ROUNDABORT OFF;IF DB_NAME()<>N'HRMS_Next_Trial_20261108' THROW 51000,'Trial only',1;BEGIN TRANSACTION; "+$mutation+[Environment]::NewLine+':r "'+$import+'"'+[Environment]::NewLine+'IF @@TRANCOUNT>0 ROLLBACK;'
    try {
        [IO.File]::WriteAllText($caseFile,$sql,[Text.UTF8Encoding]::new($true))
        $output=sqlcmd -S . -E -C -b -d $Database -i $caseFile 2>&1
        if($LASTEXITCODE -eq 0 -or ($output -join ' ') -notmatch ("Msg "+$number+",")){throw "Expected personal-import rejection $number; result: $output"}
    }finally{Remove-Item -LiteralPath $caseFile -ErrorAction SilentlyContinue}
}
# Each change is inside an outer transaction; the import's THROW/XACT_ABORT rolls it back on rejection.
Reject "UPDATE personnel.PersonalInfos SET City=N'TRIAL_CONFLICT' WHERE EmployeeId=(SELECT MIN(EmployeeId) FROM migration.PersonalInfoMap);" 51008
Reject "UPDATE migration.PersonalInfoMap SET SourceHash=0x00 WHERE EmployeeId=(SELECT MIN(EmployeeId) FROM migration.PersonalInfoMap);" 51007
Reject "DELETE migration.PersonalInfoMap WHERE EmployeeId=(SELECT MIN(EmployeeId) FROM migration.PersonalInfoMap);" 51009
$wrongDb=sqlcmd -S . -E -C -b -d tempdb -i $import 2>&1
if($LASTEXITCODE -eq 0 -or ($wrongDb -join ' ') -notmatch 'Msg 51000,'){throw 'Wrong-database guard failed.'}
$again=sqlcmd -S . -E -C -b -d $Database -i $import 2>&1
if($LASTEXITCODE -ne 0){throw "Conflict rollback/reconciliation failed: $again"}
Write-Output $again
Write-Output 'Personal import target/mapping/unmapped conflicts, rollback and wrong-database guard: PASS'
