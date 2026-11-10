-- Trial-only, repeatable EmployeeJobs import. The HRMS_BHR database is read only.
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
    THROW 51500, 'Only the named disposable HRMS trial database is allowed.', 1;
IF DB_ID(N'HRMS_BHR') IS NULL OR OBJECT_ID(N'personnel.EmployeeJobs', N'U') IS NULL
    THROW 51501, 'Source or Personnel job schema is missing.', 1;
IF OBJECT_ID(N'migration.EmployeeMap', N'U') IS NULL OR OBJECT_ID(N'migration.DepartmentMap', N'U') IS NULL
    THROW 51502, 'Core employee and department maps are required.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.EmployeeJobs j
    LEFT JOIN HRMS_BHR.dbo.Employees e ON e.EmployeeId=j.EmployeeId
    LEFT JOIN HRMS_BHR.dbo.Departments dep ON dep.DepartmentId=j.DepartmentId
    LEFT JOIN HRMS_BHR.dbo.Employees manager ON manager.EmployeeId=j.ManagerId
    LEFT JOIN HRMS_BHR.dbo.JobTitles title ON title.JobTitleId=j.JobTitleId
    LEFT JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
    LEFT JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=j.EmployeeId
    LEFT JOIN migration.DepartmentMap dm ON dm.LegacyCompanyId=e.CompanyId AND dm.LegacyDepartmentId=j.DepartmentId
    LEFT JOIN migration.EmployeeMap mm ON mm.LegacyEmployeeId=j.ManagerId
    WHERE e.EmployeeId IS NULL OR cm.CompanyId IS NULL OR em.EmployeeId IS NULL
       OR (j.DepartmentId IS NOT NULL AND (dep.DepartmentId IS NULL OR dm.DepartmentId IS NULL))
       OR (j.ManagerId IS NOT NULL AND (manager.EmployeeId IS NULL OR manager.CompanyId<>e.CompanyId OR mm.EmployeeId IS NULL))
       OR (j.JobTitleId IS NOT NULL AND title.JobTitleId IS NULL)
)
    THROW 51503, 'A job has an orphan, unmapped or cross-company reference.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.EmployeeJobs j
    LEFT JOIN HRMS_BHR.dbo.JobTitles title ON title.JobTitleId=j.JobTitleId
    WHERE j.StartDate IS NULL OR j.IsCurrent IS NULL
       OR (j.IsCurrent=1 AND j.EndDate IS NOT NULL)
       OR (j.IsCurrent=0 AND (j.EndDate IS NULL OR j.EndDate<j.StartDate))
       OR LEN(j.EmploymentType)>50 OR LEN(j.PayType)>50 OR LEN(title.TitleName)>200
)
    THROW 51504, 'A job date, state or text cannot be represented safely.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.EmployeeJobs WHERE IsCurrent=1 GROUP BY EmployeeId HAVING COUNT(*)>1)
    THROW 51505, 'An employee has more than one current job.', 1;

SELECT j.EmployeeJobId AS LegacyJobId, cm.CompanyId, em.EmployeeId,
       dm.DepartmentId, mm.EmployeeId AS ManagerEmployeeId,
       title.TitleName AS JobTitle, j.EmploymentType, j.PayType, j.PayRate,
       j.StartDate, j.EndDate, j.IsCurrent
INTO #SourceJobs
FROM HRMS_BHR.dbo.EmployeeJobs j
JOIN HRMS_BHR.dbo.Employees e ON e.EmployeeId=j.EmployeeId
JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=j.EmployeeId
LEFT JOIN migration.DepartmentMap dm ON dm.LegacyCompanyId=e.CompanyId AND dm.LegacyDepartmentId=j.DepartmentId
LEFT JOIN migration.EmployeeMap mm ON mm.LegacyEmployeeId=j.ManagerId
LEFT JOIN HRMS_BHR.dbo.JobTitles title ON title.JobTitleId=j.JobTitleId;
IF (SELECT COUNT(*) FROM #SourceJobs)<>(SELECT COUNT(*) FROM HRMS_BHR.dbo.EmployeeJobs)
    THROW 51506, 'Source job projection lost or duplicated rows.', 1;
IF EXISTS (
    SELECT 1 FROM #SourceJobs s
    JOIN personnel.Employees e ON e.Id=s.EmployeeId
    WHERE e.CompanyId<>s.CompanyId
)
    THROW 51507, 'A mapped employee belongs to the wrong company.', 1;
IF EXISTS (
    SELECT 1 FROM #SourceJobs s
    JOIN personnel.Employees m ON m.Id=s.ManagerEmployeeId
    WHERE m.CompanyId<>s.CompanyId
)
    THROW 51508, 'A mapped manager belongs to the wrong company.', 1;
IF EXISTS (
    SELECT 1 FROM #SourceJobs s
    JOIN organization.Departments d ON d.Id=s.DepartmentId
    WHERE d.CompanyId<>s.CompanyId
)
    THROW 51509, 'A mapped department belongs to the wrong company.', 1;

BEGIN TRANSACTION;
IF OBJECT_ID(N'migration.EmployeeJobMap', N'U') IS NULL
    CREATE TABLE migration.EmployeeJobMap (
        LegacyJobId int NOT NULL PRIMARY KEY,
        JobId int NOT NULL UNIQUE
    );
IF EXISTS (SELECT 1 FROM migration.EmployeeJobMap map LEFT JOIN personnel.EmployeeJobs d ON d.Id=map.JobId WHERE d.Id IS NULL)
    THROW 51510, 'A job map points to a missing destination row.', 1;
IF EXISTS (SELECT 1 FROM migration.EmployeeJobMap map LEFT JOIN #SourceJobs s ON s.LegacyJobId=map.LegacyJobId WHERE s.LegacyJobId IS NULL)
    THROW 51511, 'A mapped source job has disappeared.', 1;
IF EXISTS (
    SELECT 1 FROM #SourceJobs s
    LEFT JOIN migration.EmployeeJobMap map ON map.LegacyJobId=s.LegacyJobId
    JOIN personnel.EmployeeJobs d ON d.CompanyId=s.CompanyId AND d.EmployeeId=s.EmployeeId AND d.StartDate=s.StartDate
    WHERE map.JobId IS NULL AND d.Id<>ISNULL(map.JobId,-1)
)
    THROW 51512, 'An unmapped destination job conflicts with the source.', 1;
MERGE personnel.EmployeeJobs AS target
USING (
    SELECT s.* FROM #SourceJobs s
    LEFT JOIN migration.EmployeeJobMap map ON map.LegacyJobId=s.LegacyJobId
    WHERE map.LegacyJobId IS NULL
) AS source ON 1=0
WHEN NOT MATCHED THEN INSERT
    (CompanyId,EmployeeId,DepartmentId,ManagerEmployeeId,JobTitle,EmploymentType,PayType,PayRate,StartDate,EndDate,IsCurrent)
    VALUES (source.CompanyId,source.EmployeeId,source.DepartmentId,source.ManagerEmployeeId,
            source.JobTitle,source.EmploymentType,source.PayType,source.PayRate,
            source.StartDate,source.EndDate,source.IsCurrent)
OUTPUT source.LegacyJobId,inserted.Id INTO migration.EmployeeJobMap (LegacyJobId,JobId);
IF EXISTS (
    SELECT 1 FROM #SourceJobs s
    JOIN migration.EmployeeJobMap map ON map.LegacyJobId=s.LegacyJobId
    JOIN personnel.EmployeeJobs d ON d.Id=map.JobId
    WHERE EXISTS (
        SELECT d.CompanyId,d.EmployeeId,d.DepartmentId,d.ManagerEmployeeId,d.JobTitle,
               d.EmploymentType,d.PayType,d.PayRate,d.StartDate,d.EndDate,d.IsCurrent
        EXCEPT
        SELECT s.CompanyId,s.EmployeeId,s.DepartmentId,s.ManagerEmployeeId,s.JobTitle,
               s.EmploymentType,s.PayType,s.PayRate,s.StartDate,s.EndDate,s.IsCurrent
    )
)
    THROW 51513, 'A mapped job differs from its legacy source.', 1;
IF (SELECT COUNT(*) FROM migration.EmployeeJobMap)<>(SELECT COUNT(*) FROM #SourceJobs)
    THROW 51514, 'Job map count differs from source count.', 1;
COMMIT TRANSACTION;
SELECT (SELECT COUNT(*) FROM HRMS_BHR.dbo.EmployeeJobs) AS SourceJobs,
       (SELECT COUNT(*) FROM migration.EmployeeJobMap) AS MappedJobs,
       (SELECT COUNT(*) FROM personnel.EmployeeJobs d JOIN migration.EmployeeJobMap map ON map.JobId=d.Id) AS PresentJobs;