-- Legacy HRMS_BHR is read only. This importer is restricted to the disposable trial database.
SET NOCOUNT ON;SET XACT_ABORT ON;
SET ANSI_NULLS ON;SET ANSI_PADDING ON;SET ANSI_WARNINGS ON;SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;SET NUMERIC_ROUNDABORT OFF;SET QUOTED_IDENTIFIER ON;
IF DB_NAME()<>N'HRMS_Next_Trial_20261108' THROW 51000,'Personal import is trial only.',1;
IF OBJECT_ID(N'personnel.PersonalInfos',N'U') IS NULL THROW 51001,'Apply PersonnelPersonalInfo first.',1;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;
IF EXISTS(SELECT EmployeeId FROM HRMS_BHR.dbo.EmployeePersonalInfo GROUP BY EmployeeId HAVING COUNT(*)>1)
 THROW 51002,'Duplicate legacy personal rows require reconciliation.',1;
IF EXISTS(SELECT 1 FROM HRMS_BHR.dbo.EmployeePersonalInfo p LEFT JOIN HRMS_BHR.dbo.Employees e ON e.EmployeeId=p.EmployeeId WHERE e.EmployeeId IS NULL)
 THROW 51003,'Legacy personal row has no employee.',1;
IF EXISTS(SELECT 1 FROM HRMS_BHR.dbo.Employees e
 LEFT JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=e.EmployeeId
 LEFT JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
 LEFT JOIN personnel.Employees d ON d.Id=em.EmployeeId AND d.CompanyId=cm.CompanyId
 WHERE d.Id IS NULL) THROW 51004,'Legacy employee/company maps must be complete and consistent.',1;
IF EXISTS(SELECT 1 FROM HRMS_BHR.dbo.Employees WHERE DateOfBirth IS NOT NULL AND
 (DateOfBirth<'0622-03-22' OR CONVERT(datetime2,DateOfBirth)<>CONVERT(datetime2,CONVERT(date,DateOfBirth)) OR DateOfBirth>HireDate OR
 DateOfBirth>CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Iran Standard Time')))
 THROW 51005,'Legacy birth date is invalid or contains time.',1;


SELECT e.EmployeeId AS LegacyEmployeeId,em.EmployeeId,cm.CompanyId,p.PersonalInfoId AS SourcePersonalInfoId,CAST(NULL AS varbinary(32)) AS SourceHash,
 e.PreferredName,e.Phone,CONVERT(date,e.DateOfBirth) AS DateOfBirth,e.Gender,e.MaritalStatus,p.AddressLine1,p.AddressLine2,p.City,p.State,p.PostalCode,p.Country,p.HomePhone,p.MobilePhone,p.EmergencyContactName,p.EmergencyContactPhone,p.EmergencyContactRelation INTO #Source
FROM HRMS_BHR.dbo.Employees e
JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=e.EmployeeId
JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
LEFT JOIN HRMS_BHR.dbo.EmployeePersonalInfo p ON p.EmployeeId=e.EmployeeId;
IF EXISTS(SELECT 1 FROM #Source WHERE DATALENGTH(PreferredName)>200 OR DATALENGTH(Phone)>100 OR DATALENGTH(Gender)>40 OR DATALENGTH(MaritalStatus)>40
 OR DATALENGTH(AddressLine1)>400 OR DATALENGTH(AddressLine2)>400 OR DATALENGTH(City)>200 OR DATALENGTH(State)>200
 OR DATALENGTH(PostalCode)>40 OR DATALENGTH(Country)>200 OR DATALENGTH(HomePhone)>100 OR DATALENGTH(MobilePhone)>100
 OR DATALENGTH(EmergencyContactName)>400 OR DATALENGTH(EmergencyContactPhone)>100 OR DATALENGTH(EmergencyContactRelation)>200)
 THROW 51006,'Legacy personal fields exceed destination limits.',1;

UPDATE s SET SourceHash=HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT s.PreferredName,s.Phone,s.DateOfBirth,s.Gender,s.MaritalStatus,s.AddressLine1,s.AddressLine2,s.City,s.State,s.PostalCode,s.Country,s.HomePhone,s.MobilePhone,s.EmergencyContactName,s.EmergencyContactPhone,s.EmergencyContactRelation FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES))) FROM #Source s;
IF OBJECT_ID(N'migration.PersonalInfoMap',N'U') IS NULL
 CREATE TABLE migration.PersonalInfoMap(LegacyEmployeeId int NOT NULL PRIMARY KEY,EmployeeId int NOT NULL UNIQUE,
 CompanyId int NOT NULL,SourcePersonalInfoId int NULL,SourceHash varbinary(32) NOT NULL);
IF EXISTS(SELECT 1 FROM migration.PersonalInfoMap m LEFT JOIN #Source s ON s.LegacyEmployeeId=m.LegacyEmployeeId
 LEFT JOIN personnel.PersonalInfos p ON p.EmployeeId=m.EmployeeId AND p.CompanyId=m.CompanyId
 WHERE s.LegacyEmployeeId IS NULL OR p.EmployeeId IS NULL OR s.EmployeeId<>m.EmployeeId OR s.CompanyId<>m.CompanyId
 OR s.SourceHash<>m.SourceHash OR ISNULL(s.SourcePersonalInfoId,-1)<>ISNULL(m.SourcePersonalInfoId,-1))
 THROW 51007,'Personal source or destination mapping changed; no overwrite performed.',1;
IF EXISTS(SELECT 1 FROM migration.PersonalInfoMap m JOIN personnel.PersonalInfos p ON p.EmployeeId=m.EmployeeId AND p.CompanyId=m.CompanyId
 WHERE m.SourceHash<>HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT p.PreferredName,p.Phone,p.DateOfBirth,p.Gender,p.MaritalStatus,p.AddressLine1,p.AddressLine2,p.City,p.State,p.PostalCode,p.Country,p.HomePhone,p.MobilePhone,p.EmergencyContactName,p.EmergencyContactPhone,p.EmergencyContactRelation FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES))))
 THROW 51008,'Destination personal data differs from its import snapshot; no overwrite performed.',1;
IF EXISTS(SELECT 1 FROM #Source s JOIN personnel.PersonalInfos p ON p.EmployeeId=s.EmployeeId
 LEFT JOIN migration.PersonalInfoMap m ON m.LegacyEmployeeId=s.LegacyEmployeeId WHERE m.LegacyEmployeeId IS NULL)
 THROW 51009,'Unmapped destination personal data exists; no overwrite performed.',1;
INSERT personnel.PersonalInfos(EmployeeId,CompanyId,PreferredName,Phone,DateOfBirth,Gender,MaritalStatus,AddressLine1,AddressLine2,City,State,PostalCode,Country,HomePhone,MobilePhone,EmergencyContactName,EmergencyContactPhone,EmergencyContactRelation)
SELECT s.EmployeeId,s.CompanyId,s.PreferredName,s.Phone,s.DateOfBirth,s.Gender,s.MaritalStatus,s.AddressLine1,s.AddressLine2,s.City,s.State,s.PostalCode,s.Country,s.HomePhone,s.MobilePhone,s.EmergencyContactName,s.EmergencyContactPhone,s.EmergencyContactRelation FROM #Source s
LEFT JOIN migration.PersonalInfoMap m ON m.LegacyEmployeeId=s.LegacyEmployeeId WHERE m.LegacyEmployeeId IS NULL;
INSERT migration.PersonalInfoMap(LegacyEmployeeId,EmployeeId,CompanyId,SourcePersonalInfoId,SourceHash)
SELECT s.LegacyEmployeeId,s.EmployeeId,s.CompanyId,s.SourcePersonalInfoId,s.SourceHash FROM #Source s
LEFT JOIN migration.PersonalInfoMap m ON m.LegacyEmployeeId=s.LegacyEmployeeId WHERE m.LegacyEmployeeId IS NULL;
SELECT COUNT(*) AS SourceEmployees,COUNT(m.EmployeeId) AS Mapped,COUNT(p.EmployeeId) AS Present,
 SUM(CASE WHEN s.SourceHash<>h.Value THEN 1 ELSE 0 END) AS ChangedValues
FROM #Source s LEFT JOIN migration.PersonalInfoMap m ON m.LegacyEmployeeId=s.LegacyEmployeeId
LEFT JOIN personnel.PersonalInfos p ON p.EmployeeId=m.EmployeeId AND p.CompanyId=m.CompanyId
OUTER APPLY (SELECT HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT p.PreferredName,p.Phone,p.DateOfBirth,p.Gender,p.MaritalStatus,p.AddressLine1,p.AddressLine2,p.City,p.State,p.PostalCode,p.Country,p.HomePhone,p.MobilePhone,p.EmergencyContactName,p.EmergencyContactPhone,p.EmergencyContactRelation FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES))) AS Value) h;
COMMIT;
