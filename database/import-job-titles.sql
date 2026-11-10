-- Trial-only repeatable import. HRMS_BHR is read-only.
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'HRMS_Next_Trial_20261108'
    THROW 51600, 'Only the named disposable trial database is allowed.', 1;
IF DB_ID(N'HRMS_BHR') IS NULL OR OBJECT_ID(N'organization.JobTitles', N'U') IS NULL
    THROW 51601, 'Source or destination title table is missing.', 1;
IF OBJECT_ID(N'migration.CompanyMap', N'U') IS NULL
    THROW 51602, 'Company map is required.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.Companies c LEFT JOIN migration.CompanyMap m ON m.LegacyCompanyId=c.CompanyId WHERE m.CompanyId IS NULL)
    THROW 51603, 'A legacy company is not mapped.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.JobTitles WHERE LEN(LTRIM(RTRIM(TitleName))) NOT BETWEEN 1 AND 200 OR LEN(Description)>1000)
    THROW 51604, 'A title cannot be represented.', 1;
IF EXISTS (SELECT LTRIM(RTRIM(TitleName)) FROM HRMS_BHR.dbo.JobTitles GROUP BY LTRIM(RTRIM(TitleName)) HAVING COUNT(*)>1)
    THROW 51605, 'Legacy title names are duplicated after trimming.', 1;

SELECT cm.LegacyCompanyId, cm.CompanyId, t.JobTitleId AS LegacyJobTitleId,
       LTRIM(RTRIM(t.TitleName)) AS Name, NULLIF(LTRIM(RTRIM(t.Description)), N'') AS Description
INTO #SourceTitles
FROM migration.CompanyMap cm CROSS JOIN HRMS_BHR.dbo.JobTitles t;
IF (SELECT COUNT(*) FROM #SourceTitles)<>(SELECT COUNT(*) FROM migration.CompanyMap)*(SELECT COUNT(*) FROM HRMS_BHR.dbo.JobTitles)
    THROW 51606, 'Title projection lost or duplicated rows.', 1;
BEGIN TRANSACTION;
IF OBJECT_ID(N'migration.JobTitleMap', N'U') IS NULL
    CREATE TABLE migration.JobTitleMap (
        LegacyCompanyId int NOT NULL, LegacyJobTitleId int NOT NULL, JobTitleId int NOT NULL UNIQUE,
        CONSTRAINT PK_JobTitleMap PRIMARY KEY (LegacyCompanyId, LegacyJobTitleId));
IF EXISTS (SELECT 1 FROM migration.JobTitleMap m LEFT JOIN organization.JobTitles d ON d.Id=m.JobTitleId WHERE d.Id IS NULL)
    THROW 51607, 'A title map points to a missing destination row.', 1;
IF EXISTS (SELECT 1 FROM migration.JobTitleMap m LEFT JOIN #SourceTitles s ON s.LegacyCompanyId=m.LegacyCompanyId AND s.LegacyJobTitleId=m.LegacyJobTitleId WHERE s.LegacyJobTitleId IS NULL)
    THROW 51608, 'A mapped legacy title has disappeared.', 1;
IF EXISTS (
    SELECT 1 FROM #SourceTitles s
    LEFT JOIN migration.JobTitleMap m ON m.LegacyCompanyId=s.LegacyCompanyId AND m.LegacyJobTitleId=s.LegacyJobTitleId
    JOIN organization.JobTitles d ON d.CompanyId=s.CompanyId AND d.Name=s.Name
    WHERE m.JobTitleId IS NULL)
    THROW 51609, 'An unmapped destination title conflicts with the legacy catalog.', 1;
MERGE organization.JobTitles AS target
USING (
    SELECT s.* FROM #SourceTitles s
    LEFT JOIN migration.JobTitleMap m ON m.LegacyCompanyId=s.LegacyCompanyId AND m.LegacyJobTitleId=s.LegacyJobTitleId
    WHERE m.JobTitleId IS NULL
) AS source ON 1=0
WHEN NOT MATCHED THEN INSERT (CompanyId,Name,Description,IsActive)
    VALUES (source.CompanyId,source.Name,source.Description,1)
OUTPUT source.LegacyCompanyId,source.LegacyJobTitleId,inserted.Id
INTO migration.JobTitleMap (LegacyCompanyId,LegacyJobTitleId,JobTitleId);
IF EXISTS (
    SELECT 1 FROM #SourceTitles s
    JOIN migration.JobTitleMap m ON m.LegacyCompanyId=s.LegacyCompanyId AND m.LegacyJobTitleId=s.LegacyJobTitleId
    JOIN organization.JobTitles d ON d.Id=m.JobTitleId
    WHERE EXISTS (SELECT d.CompanyId,d.Name,d.Description,d.IsActive EXCEPT SELECT s.CompanyId,s.Name,s.Description,CONVERT(bit,1)))
    THROW 51610, 'A mapped title differs from its legacy source.', 1;
IF (SELECT COUNT(*) FROM migration.JobTitleMap)<>(SELECT COUNT(*) FROM #SourceTitles)
    THROW 51611, 'Title map count differs from source count.', 1;
COMMIT TRANSACTION;
SELECT (SELECT COUNT(*) FROM #SourceTitles) AS SourceTitles,
       (SELECT COUNT(*) FROM migration.JobTitleMap) AS MappedTitles,
       (SELECT COUNT(*) FROM organization.JobTitles d JOIN migration.JobTitleMap m ON m.JobTitleId=d.Id) AS PresentTitles;
