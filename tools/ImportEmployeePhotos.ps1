param([string]$Database='HRMS_Next_Trial_20261108',[string]$LegacyPhotoRoot='D:\SourceCodes\GitProjects\HRMS_BHR\src\HRMS.Web\App_Data\photos')
$ErrorActionPreference='Stop'
if($Database -ne 'HRMS_Next_Trial_20261108'){throw 'Only the named disposable trial database is allowed.'}
$root=(Resolve-Path -LiteralPath $LegacyPhotoRoot).Path.TrimEnd('\')+'\'
$connection=[Data.SqlClient.SqlConnection]::new("Server=.;Database=$Database;Integrated Security=True;Encrypt=False;TrustServerCertificate=True")
$transaction=$null
try{
 $connection.Open()
 $query=$connection.CreateCommand()
 $query.CommandText=@'
SELECT s.EmployeeId AS LegacyEmployeeId,s.AvatarPath,s.ModifiedDate,m.EmployeeId,p.CompanyId,cm.CompanyId AS MappedCompanyId
FROM HRMS_BHR.dbo.Employees s LEFT JOIN migration.EmployeeMap m ON m.LegacyEmployeeId=s.EmployeeId
LEFT JOIN personnel.Employees p ON p.Id=m.EmployeeId LEFT JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=s.CompanyId
WHERE NULLIF(LTRIM(RTRIM(s.AvatarPath)),'') IS NOT NULL;
'@
 $table=[Data.DataTable]::new();$reader=$query.ExecuteReader()
 try{$table.Load($reader)}finally{$reader.Dispose();$query.Dispose()}
 $transaction=$connection.BeginTransaction()
 $schema=$connection.CreateCommand();$schema.Transaction=$transaction
 $schema.CommandText=@'
IF OBJECT_ID(N'migration.EmployeePhotoMap',N'U') IS NULL
 CREATE TABLE migration.EmployeePhotoMap(LegacyEmployeeId int NOT NULL PRIMARY KEY,EmployeeId int NOT NULL UNIQUE,
 CompanyId int NOT NULL,SourceFileName nvarchar(500) NOT NULL,Sha256 nvarchar(64) NOT NULL,
 LegacyModifiedLocal datetime2 NOT NULL,ImportedAtUtc datetimeoffset NOT NULL);
'@
 $schema.ExecuteNonQuery() | Out-Null;$schema.Dispose();$compared=0
 foreach($row in $table.Rows){
  if($row.EmployeeId -is [DBNull] -or $row.CompanyId -is [DBNull] -or $row.MappedCompanyId -is [DBNull] -or $row.CompanyId -ne $row.MappedCompanyId){throw 'Photo employee/company mapping is missing or inconsistent.'}
  $name=[string]$row.AvatarPath
  if($name -ne [IO.Path]::GetFileName($name) -or [IO.Path]::IsPathRooted($name)){throw 'Unsafe legacy photo name.'}
  $path=[IO.Path]::GetFullPath((Join-Path $root $name))
  if(!$path.StartsWith($root,[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $path -PathType Leaf)){throw 'Referenced legacy photo is missing or outside its root.'}
  $length=(Get-Item -LiteralPath $path).Length
  if($length -lt 20 -or $length -gt 2097152){throw 'Legacy photo size is invalid.'}
  $bytes=[IO.File]::ReadAllBytes($path);$ext=[IO.Path]::GetExtension($name).ToLowerInvariant()
  $png=$ext -eq '.png' -and [BitConverter]::ToString($bytes,0,8) -eq '89-50-4E-47-0D-0A-1A-0A' -and [BitConverter]::ToString($bytes,$bytes.Length-8,8) -eq '49-45-4E-44-AE-42-60-82'
  $jpeg=$ext -in @('.jpg','.jpeg') -and $bytes[0] -eq 255 -and $bytes[1] -eq 216 -and $bytes[2] -eq 255 -and $bytes[-2] -eq 255 -and $bytes[-1] -eq 217
  if(!$png -and !$jpeg){throw 'Legacy photo signature is invalid.'}
  $contentType=if($png){'image/png'}else{'image/jpeg'}
  $hasher=[Security.Cryptography.SHA256]::Create()
  try{$hash=([BitConverter]::ToString($hasher.ComputeHash($bytes))).Replace('-','')}finally{$hasher.Dispose()}
  if($row.ModifiedDate -is [DBNull]){throw 'Legacy photo timestamp is missing.'}
  $local=[DateTime]::SpecifyKind([DateTime]$row.ModifiedDate,[DateTimeKind]::Unspecified)
  $command=$connection.CreateCommand();$command.Transaction=$transaction
  $command.CommandText=@'
IF EXISTS(SELECT 1 FROM migration.EmployeePhotoMap WITH(UPDLOCK,HOLDLOCK) WHERE LegacyEmployeeId=@legacy)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM migration.EmployeePhotoMap m JOIN personnel.EmployeePhotos p ON p.EmployeeId=m.EmployeeId
 WHERE m.LegacyEmployeeId=@legacy AND m.EmployeeId=@employee AND m.CompanyId=@company AND m.SourceFileName=@name
 AND m.Sha256=@hash AND p.CompanyId=@company AND p.Sha256=@hash AND p.ContentType=@type AND p.UploadedAtUtc=m.ImportedAtUtc
 AND CONVERT(varchar(64),HASHBYTES('SHA2_256',p.Bytes),2)=@hash)
 THROW 51701,'Mapped source/destination photo changed; review required.',1;
END ELSE BEGIN
 IF EXISTS(SELECT 1 FROM personnel.EmployeePhotos WITH(UPDLOCK,HOLDLOCK) WHERE EmployeeId=@employee)
 THROW 51702,'Unmapped destination photo exists; refusing overwrite.',1;
 INSERT personnel.EmployeePhotos(EmployeeId,CompanyId,Bytes,ContentType,Sha256,UploadedAtUtc)
 VALUES(@employee,@company,@bytes,@type,@hash,@utc);
 INSERT migration.EmployeePhotoMap(LegacyEmployeeId,EmployeeId,CompanyId,SourceFileName,Sha256,LegacyModifiedLocal,ImportedAtUtc)
 VALUES(@legacy,@employee,@company,@name,@hash,@local,@utc);
END;
'@
  $command.Parameters.AddWithValue('@legacy',[int]$row.LegacyEmployeeId) | Out-Null
  $command.Parameters.AddWithValue('@employee',[int]$row.EmployeeId) | Out-Null
  $command.Parameters.AddWithValue('@company',[int]$row.CompanyId) | Out-Null
  $command.Parameters.AddWithValue('@name',$name) | Out-Null
  $command.Parameters.AddWithValue('@type',$contentType) | Out-Null
  $command.Parameters.AddWithValue('@hash',$hash) | Out-Null
  $command.Parameters.Add('@bytes',[Data.SqlDbType]::VarBinary,-1).Value=$bytes
  $command.Parameters.Add('@utc',[Data.SqlDbType]::DateTimeOffset).Value=[DateTimeOffset]::UtcNow
  $command.Parameters.Add('@local',[Data.SqlDbType]::DateTime2).Value=$local
  $command.ExecuteNonQuery() | Out-Null;$command.Dispose();$compared++
 }
 $count=$connection.CreateCommand();$count.Transaction=$transaction;$count.CommandText='SELECT COUNT(*) FROM migration.EmployeePhotoMap'
 $mapped=[int]$count.ExecuteScalar();$count.Dispose()
 if($mapped -ne $table.Rows.Count -or $compared -ne $mapped){throw 'Photo reconciliation counts differ.'}
 $transaction.Commit();$transaction.Dispose();$transaction=$null
 Write-Output "Photo trial import: source=$($table.Rows.Count) mapped=$mapped checksumCompared=$compared"
}finally{if($transaction){$transaction.Rollback();$transaction.Dispose()};$connection.Dispose()}
