-- HRMS_BHR is read only. Employment status is separate from approved Leave requests.
SET NOCOUNT ON;
SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'HRMS_Next_Trial_20261108' THROW 51000,'Status import is restricted to the named trial database.',1;
IF DB_ID(N'HRMS_BHR') IS NULL THROW 51001,'Legacy database missing.',1;
IF OBJECT_ID(N'personnel.EmploymentStatusChanges',N'U') IS NULL OR OBJECT_ID(N'migration.EmployeeMap',N'U') IS NULL
    THROW 51002,'Status migration and core maps must exist first.',1;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;
IF OBJECT_ID(N'migration.EmploymentStatusMap',N'U') IS NULL
    CREATE TABLE migration.EmploymentStatusMap(
        LegacyEmployeeId int NOT NULL PRIMARY KEY, EmployeeId int NOT NULL UNIQUE,
        CompanyId int NOT NULL, StatusChangeId bigint NOT NULL UNIQUE,
        SourceStatus nvarchar(20) NOT NULL, SnapshotHash binary(32) NOT NULL);

SELECT e.EmployeeId LegacyEmployeeId, m.EmployeeId, cm.CompanyId,
       e.EmploymentStatus SourceStatus, e.TerminationDate SourceTerminationDate,
       CONVERT(bit,CASE WHEN e.EmploymentStatus=N'OnLeave' THEN 1 ELSE 0 END) IsOnLeave,
       HASHBYTES('SHA2_256',CONVERT(nvarchar(max),CONCAT(e.EmployeeId,N'|',e.CompanyId,N'|',m.EmployeeId,N'|',cm.CompanyId,N'|',e.EmploymentStatus,N'|',ISNULL(CONVERT(nvarchar(10),e.TerminationDate,23),N'NULL')))) SnapshotHash
INTO #statusSource
FROM HRMS_BHR.dbo.Employees e
LEFT JOIN migration.EmployeeMap m ON m.LegacyEmployeeId=e.EmployeeId
LEFT JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId;
IF EXISTS(SELECT 1 FROM #statusSource WHERE SourceStatus IS NULL OR SourceStatus NOT IN(N'Active',N'OnLeave') OR SourceTerminationDate IS NOT NULL)
    THROW 51003,'Only supported nonterminated Active/OnLeave baselines can be imported; departure requires its own validated transfer.',1;
IF EXISTS(SELECT 1 FROM #statusSource s LEFT JOIN personnel.Employees e ON e.Id=s.EmployeeId AND e.CompanyId=s.CompanyId WHERE e.Id IS NULL)
    THROW 51004,'Missing or inconsistent company/employee provenance map.',1;
IF EXISTS(SELECT 1 FROM migration.EmploymentStatusMap m LEFT JOIN #statusSource s ON s.LegacyEmployeeId=m.LegacyEmployeeId WHERE s.LegacyEmployeeId IS NULL)
    THROW 51005,'Status provenance contains a missing source record.',1;
IF EXISTS(SELECT 1 FROM #statusSource s JOIN migration.EmploymentStatusMap m ON m.LegacyEmployeeId=s.LegacyEmployeeId
          WHERE m.EmployeeId<>s.EmployeeId OR m.CompanyId<>s.CompanyId OR m.SourceStatus<>s.SourceStatus OR m.SnapshotHash<>s.SnapshotHash)
    THROW 51007,'Legacy status or provenance changed; import refuses to overwrite the snapshot.',1;
IF EXISTS(SELECT 1 FROM #statusSource s JOIN migration.EmploymentStatusMap m ON m.LegacyEmployeeId=s.LegacyEmployeeId
          JOIN personnel.Employees e ON e.Id=s.EmployeeId AND e.CompanyId=s.CompanyId
          LEFT JOIN personnel.EmploymentStatusChanges h ON h.Id=m.StatusChangeId AND h.EmployeeId=s.EmployeeId AND h.CompanyId=s.CompanyId
          WHERE e.IsOnLeave<>s.IsOnLeave OR e.TerminationDate IS NOT NULL OR h.Id IS NULL OR h.ToStatus<>s.SourceStatus
             OR h.EffectiveDate IS NOT NULL OR h.FromStatus IS NOT NULL OR h.ActorUserId IS NOT NULL
             OR (SELECT COUNT(*) FROM personnel.EmploymentStatusChanges allHistory WHERE allHistory.EmployeeId=s.EmployeeId)<>1)
    THROW 51008,'Target status/history changed after import; reconcile explicitly before reimport.',1;
IF EXISTS(SELECT 1 FROM #statusSource s JOIN personnel.Employees e ON e.Id=s.EmployeeId AND e.CompanyId=s.CompanyId
          WHERE NOT EXISTS(SELECT 1 FROM migration.EmploymentStatusMap m WHERE m.LegacyEmployeeId=s.LegacyEmployeeId)
          AND (e.IsOnLeave<>0 OR e.TerminationDate IS NOT NULL OR EXISTS(SELECT 1 FROM personnel.EmploymentStatusChanges h WHERE h.EmployeeId=s.EmployeeId)))
    THROW 51009,'Unmapped target already contains status edits/history; refusing to adopt it.',1;

UPDATE e SET IsOnLeave=s.IsOnLeave
FROM personnel.Employees e JOIN #statusSource s ON e.Id=s.EmployeeId AND e.CompanyId=s.CompanyId
WHERE e.IsOnLeave<>s.IsOnLeave AND NOT EXISTS(SELECT 1 FROM migration.EmploymentStatusMap m WHERE m.LegacyEmployeeId=s.LegacyEmployeeId);
MERGE personnel.EmploymentStatusChanges AS target
USING (SELECT s.* FROM #statusSource s WHERE NOT EXISTS(SELECT 1 FROM migration.EmploymentStatusMap m WHERE m.LegacyEmployeeId=s.LegacyEmployeeId)) AS source
ON 1=0
WHEN NOT MATCHED THEN INSERT(CompanyId,EmployeeId,EffectiveDate,FromStatus,ToStatus,ActorUserId,AtUtc)
VALUES(source.CompanyId,source.EmployeeId,NULL,NULL,source.SourceStatus,NULL,SYSDATETIMEOFFSET())
OUTPUT source.LegacyEmployeeId,inserted.EmployeeId,inserted.CompanyId,inserted.Id,source.SourceStatus,source.SnapshotHash
INTO migration.EmploymentStatusMap(LegacyEmployeeId,EmployeeId,CompanyId,StatusChangeId,SourceStatus,SnapshotHash);

SELECT CONCAT(N'Employment status import: source=',(SELECT COUNT(*) FROM #statusSource),N' mapped=',(SELECT COUNT(*) FROM migration.EmploymentStatusMap),
    N' compared=',(SELECT COUNT(*) FROM #statusSource s JOIN migration.EmploymentStatusMap m ON m.LegacyEmployeeId=s.LegacyEmployeeId
    JOIN personnel.Employees e ON e.Id=m.EmployeeId AND e.CompanyId=m.CompanyId WHERE e.IsOnLeave=s.IsOnLeave AND m.SnapshotHash=s.SnapshotHash),
    N' onleave=',(SELECT COUNT(*) FROM #statusSource WHERE IsOnLeave=1));
COMMIT;