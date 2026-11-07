-- Run with sqlcmd -b -d <new nonproduction database> -i next/import-core.sql.
-- Source HRMS_BHR.dbo is read only. This first transfer covers companies,
-- departments and current employee profile fields; later modules need separate tools.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT LIKE N'HRMS[_]Next[_]%'
    THROW 51000, 'Destination must be a separate HRMS_Next_ database.', 1;
IF DB_ID(N'HRMS_BHR') IS NULL
    THROW 51001, 'Legacy HRMS_BHR database was not found on this SQL Server.', 1;

SELECT N'MissingCompanyOrNumber' AS Issue, COUNT(*) AS InvalidCount FROM HRMS_BHR.dbo.Employees
WHERE CompanyId IS NULL OR EmployeeNumber IS NULL OR LTRIM(RTRIM(EmployeeNumber))=N''
UNION ALL
SELECT N'CurrentJobCount', COUNT(*) FROM (
    SELECT e.EmployeeId FROM HRMS_BHR.dbo.Employees e
    LEFT JOIN HRMS_BHR.dbo.EmployeeJobs j ON j.EmployeeId=e.EmployeeId AND j.IsCurrent=1
    GROUP BY e.EmployeeId HAVING COUNT(j.EmployeeJobId)<>1
) invalid
UNION ALL
SELECT N'CurrentJobDepartment', COUNT(*) FROM HRMS_BHR.dbo.EmployeeJobs j
LEFT JOIN HRMS_BHR.dbo.Departments d ON d.DepartmentId=j.DepartmentId
WHERE j.IsCurrent=1 AND d.DepartmentId IS NULL
UNION ALL
SELECT N'DuplicateEmployeeNumber', COUNT(*) FROM (
    SELECT CompanyId,EmployeeNumber FROM HRMS_BHR.dbo.Employees
    GROUP BY CompanyId,EmployeeNumber HAVING COUNT(*)>1
) invalid;

IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.Employees WHERE CompanyId IS NULL OR EmployeeNumber IS NULL OR LTRIM(RTRIM(EmployeeNumber)) = N'')
    THROW 51002, 'Legacy employees have missing company or employee number.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.Employees e LEFT JOIN HRMS_BHR.dbo.Companies c ON c.CompanyId=e.CompanyId WHERE c.CompanyId IS NULL)
    THROW 51003, 'Legacy employees reference a missing company.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.Employees e LEFT JOIN HRMS_BHR.dbo.EmployeeJobs j ON j.EmployeeId=e.EmployeeId AND j.IsCurrent=1 GROUP BY e.EmployeeId HAVING COUNT(j.EmployeeJobId)<>1 OR SUM(CASE WHEN j.DepartmentId IS NULL THEN 1 ELSE 0 END)>0)
    THROW 51004, 'Every employee needs exactly one current job with a department.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.EmployeeJobs j LEFT JOIN HRMS_BHR.dbo.Departments d ON d.DepartmentId=j.DepartmentId WHERE j.IsCurrent=1 AND d.DepartmentId IS NULL)
    THROW 51005, 'A current job references a missing department.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.Departments d LEFT JOIN HRMS_BHR.dbo.Departments p ON p.DepartmentId=d.ParentDepartmentId WHERE d.ParentDepartmentId IS NOT NULL AND p.DepartmentId IS NULL)
    THROW 51006, 'A legacy department references a missing parent.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.Departments GROUP BY DepartmentName HAVING COUNT(*)>1)
    THROW 51007, 'Legacy department names are not unique; resolve before import.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.Employees WHERE LEN(EmployeeNumber)>50 OR LEN(FirstName)>100 OR LEN(LastName)>100 OR LEN(Email)>256)
    THROW 51008, 'Legacy employee fields exceed destination length limits.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.Employees GROUP BY CompanyId,EmployeeNumber HAVING COUNT(*)>1)
    THROW 51009, 'Legacy employee numbers are duplicated within a company.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.Companies WHERE LEN(CompanyName)>200)
    THROW 51010, 'Legacy company name exceeds destination length.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.Departments WHERE LEN(DepartmentName)>200)
    THROW 51011, 'Legacy department name exceeds destination length.', 1;

BEGIN TRANSACTION;
IF SCHEMA_ID(N'migration') IS NULL EXEC(N'CREATE SCHEMA [migration]');
IF OBJECT_ID(N'migration.CompanyMap',N'U') IS NULL
    CREATE TABLE migration.CompanyMap (LegacyCompanyId int NOT NULL PRIMARY KEY, CompanyId int NOT NULL UNIQUE);
IF OBJECT_ID(N'migration.DepartmentMap',N'U') IS NULL
    CREATE TABLE migration.DepartmentMap (LegacyCompanyId int NOT NULL, LegacyDepartmentId int NOT NULL, DepartmentId int NOT NULL UNIQUE, CONSTRAINT PK_DepartmentMap PRIMARY KEY (LegacyCompanyId,LegacyDepartmentId));
IF OBJECT_ID(N'migration.EmployeeMap',N'U') IS NULL
    CREATE TABLE migration.EmployeeMap (LegacyEmployeeId int NOT NULL PRIMARY KEY, EmployeeId int NOT NULL UNIQUE);

IF EXISTS (SELECT 1 FROM migration.CompanyMap m LEFT JOIN organization.Companies d ON d.Id=m.CompanyId WHERE d.Id IS NULL)
    THROW 51013, 'A company import map points to a missing destination row.', 1;
IF EXISTS (SELECT 1 FROM migration.DepartmentMap m LEFT JOIN organization.Departments d ON d.Id=m.DepartmentId WHERE d.Id IS NULL)
    THROW 51014, 'A department import map points to a missing destination row.', 1;
IF EXISTS (SELECT 1 FROM migration.EmployeeMap m LEFT JOIN personnel.Employees d ON d.Id=m.EmployeeId WHERE d.Id IS NULL)
    THROW 51015, 'An employee import map points to a missing destination row.', 1;

IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.Companies c JOIN organization.Companies d ON d.Name=c.CompanyName LEFT JOIN migration.CompanyMap m ON m.LegacyCompanyId=c.CompanyId WHERE m.LegacyCompanyId IS NULL)
    THROW 51012, 'A destination company name exists without an import map.', 1;

MERGE organization.Companies AS target
USING (
    SELECT c.CompanyId, c.CompanyName FROM HRMS_BHR.dbo.Companies c
    LEFT JOIN migration.CompanyMap m ON m.LegacyCompanyId=c.CompanyId
    WHERE m.LegacyCompanyId IS NULL
) AS source ON 1=0
WHEN NOT MATCHED THEN INSERT (Name) VALUES (source.CompanyName)
OUTPUT source.CompanyId, inserted.Id INTO migration.CompanyMap (LegacyCompanyId,CompanyId);

MERGE organization.Departments AS target
USING (
    SELECT c.CompanyId AS LegacyCompanyId, d.DepartmentId AS LegacyDepartmentId,
           cm.CompanyId AS CompanyId, d.DepartmentName AS Name
    FROM HRMS_BHR.dbo.Companies c CROSS JOIN HRMS_BHR.dbo.Departments d
    JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=c.CompanyId
    LEFT JOIN migration.DepartmentMap dm ON dm.LegacyCompanyId=c.CompanyId AND dm.LegacyDepartmentId=d.DepartmentId
    WHERE dm.LegacyDepartmentId IS NULL
) AS source ON 1=0
WHEN NOT MATCHED THEN INSERT (CompanyId,Name) VALUES (source.CompanyId,source.Name)
OUTPUT source.LegacyCompanyId,source.LegacyDepartmentId,inserted.Id
INTO migration.DepartmentMap (LegacyCompanyId,LegacyDepartmentId,DepartmentId);

UPDATE destination SET ParentId=parentMap.DepartmentId
FROM organization.Departments destination
JOIN migration.DepartmentMap map ON map.DepartmentId=destination.Id
JOIN HRMS_BHR.dbo.Departments legacy ON legacy.DepartmentId=map.LegacyDepartmentId
LEFT JOIN migration.DepartmentMap parentMap ON parentMap.LegacyCompanyId=map.LegacyCompanyId AND parentMap.LegacyDepartmentId=legacy.ParentDepartmentId
WHERE ISNULL(destination.ParentId,-1)<>ISNULL(parentMap.DepartmentId,-1);

MERGE personnel.Employees AS target
USING (
    SELECT e.EmployeeId, cm.CompanyId, dm.DepartmentId, e.EmployeeNumber,
           e.FirstName,e.LastName,e.Email,e.HireDate,e.TerminationDate
    FROM HRMS_BHR.dbo.Employees e
    JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
    JOIN HRMS_BHR.dbo.EmployeeJobs j ON j.EmployeeId=e.EmployeeId AND j.IsCurrent=1
    JOIN migration.DepartmentMap dm ON dm.LegacyCompanyId=e.CompanyId AND dm.LegacyDepartmentId=j.DepartmentId
    LEFT JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=e.EmployeeId
    WHERE em.LegacyEmployeeId IS NULL
) AS source ON 1=0
WHEN NOT MATCHED THEN INSERT (CompanyId,DepartmentId,EmployeeNumber,FirstName,LastName,Email,HireDate,TerminationDate)
VALUES (source.CompanyId,source.DepartmentId,source.EmployeeNumber,source.FirstName,source.LastName,source.Email,source.HireDate,source.TerminationDate)
OUTPUT source.EmployeeId,inserted.Id INTO migration.EmployeeMap (LegacyEmployeeId,EmployeeId);

COMMIT TRANSACTION;

SELECT N'Companies' AS Entity, (SELECT COUNT(*) FROM HRMS_BHR.dbo.Companies) AS SourceCount,
       (SELECT COUNT(*) FROM migration.CompanyMap) AS MappedCount;
SELECT N'Departments per company' AS Entity,
       (SELECT COUNT(*) FROM HRMS_BHR.dbo.Departments)*(SELECT COUNT(*) FROM HRMS_BHR.dbo.Companies) AS SourceCount,
       (SELECT COUNT(*) FROM migration.DepartmentMap) AS MappedCount;
SELECT N'Employees' AS Entity, (SELECT COUNT(*) FROM HRMS_BHR.dbo.Employees) AS SourceCount,
       (SELECT COUNT(*) FROM migration.EmployeeMap) AS MappedCount;
SELECT COUNT(*) AS ChangedCompanyRows FROM HRMS_BHR.dbo.Companies c
JOIN migration.CompanyMap map ON map.LegacyCompanyId=c.CompanyId
JOIN organization.Companies d ON d.Id=map.CompanyId
WHERE d.Name<>c.CompanyName;
SELECT COUNT(*) AS ChangedDepartmentRows FROM HRMS_BHR.dbo.Departments legacy
JOIN migration.DepartmentMap map ON map.LegacyDepartmentId=legacy.DepartmentId
JOIN organization.Departments d ON d.Id=map.DepartmentId
LEFT JOIN migration.DepartmentMap parentMap ON parentMap.LegacyCompanyId=map.LegacyCompanyId AND parentMap.LegacyDepartmentId=legacy.ParentDepartmentId
WHERE d.Name<>legacy.DepartmentName OR ISNULL(d.ParentId,-1)<>ISNULL(parentMap.DepartmentId,-1);
SELECT COUNT(*) AS ChangedEmployeeRows FROM HRMS_BHR.dbo.Employees e
JOIN migration.EmployeeMap map ON map.LegacyEmployeeId=e.EmployeeId
JOIN personnel.Employees d ON d.Id=map.EmployeeId
JOIN HRMS_BHR.dbo.EmployeeJobs j ON j.EmployeeId=e.EmployeeId AND j.IsCurrent=1
JOIN migration.DepartmentMap dm ON dm.LegacyCompanyId=e.CompanyId AND dm.LegacyDepartmentId=j.DepartmentId
WHERE d.EmployeeNumber<>e.EmployeeNumber OR d.FirstName<>e.FirstName OR d.LastName<>e.LastName
   OR ISNULL(d.Email,N'')<>ISNULL(e.Email,N'') OR d.HireDate<>e.HireDate
   OR ISNULL(d.TerminationDate,CONVERT(date,'00010101'))<>ISNULL(e.TerminationDate,CONVERT(date,'00010101'))
   OR d.DepartmentId<>dm.DepartmentId OR d.CompanyId<>(SELECT CompanyId FROM migration.CompanyMap WHERE LegacyCompanyId=e.CompanyId);
