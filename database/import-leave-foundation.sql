-- Trial-only, repeatable Leave foundation transfer. Never run against HRMS_BHR.
-- Requires import-core.sql and the Leave schema in the named destination.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT LIKE N'HRMS[_]Next[_]Trial[_]%'
    THROW 51100, 'Destination must be an HRMS_Next_Trial_ database.', 1;
IF DB_ID(N'HRMS_BHR') IS NULL
    THROW 51101, 'Legacy source database was not found.', 1;
IF OBJECT_ID(N'migration.CompanyMap', N'U') IS NULL OR OBJECT_ID(N'migration.EmployeeMap', N'U') IS NULL
    THROW 51102, 'Run import-core.sql before Leave import.', 1;
IF OBJECT_ID(N'leave.Policies', N'U') IS NULL OR OBJECT_ID(N'leave.Balances', N'U') IS NULL
    THROW 51103, 'Apply the Leave migration before Leave import.', 1;

IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffPolicies p
    WHERE p.CompanyId IS NULL OR p.PolicyName IS NULL OR LTRIM(RTRIM(p.PolicyName))=N''
       OR LEN(p.PolicyName)>200 OR LEN(p.Category)>50 OR LEN(p.Description)>1000
       OR p.AnnualHours IS NULL OR p.AnnualHours<0 OR p.AnnualHours>99999999
       OR p.Active IS NULL
)
    THROW 51104, 'Legacy Leave policy fields cannot be represented safely.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffPolicies p
    LEFT JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=p.CompanyId
    WHERE cm.CompanyId IS NULL
)
    THROW 51105, 'A Leave policy company has no import map.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffPolicies
    GROUP BY CompanyId, PolicyName HAVING COUNT(*)>1
)
    THROW 51106, 'Legacy Leave policy names are duplicated within a company.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffBalances b
    WHERE b.Year NOT BETWEEN 1900 AND 2500
       OR b.AccruedHours IS NULL OR b.UsedHours IS NULL OR b.LastUpdated IS NULL
       OR b.AccruedHours<0 OR b.UsedHours<0
       OR b.AccruedHours>99999999 OR b.UsedHours>99999999
)
    THROW 51107, 'Legacy Leave balance values cannot be represented safely.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffBalances
    GROUP BY EmployeeId, PolicyId, Year HAVING COUNT(*)>1
)
    THROW 51108, 'Legacy Leave balances have duplicate employee/policy/year keys.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffBalances b
    LEFT JOIN HRMS_BHR.dbo.Employees e ON e.EmployeeId=b.EmployeeId
    LEFT JOIN HRMS_BHR.dbo.TimeOffPolicies p ON p.PolicyId=b.PolicyId
    LEFT JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=b.EmployeeId
    WHERE e.EmployeeId IS NULL OR p.PolicyId IS NULL OR em.EmployeeId IS NULL
       OR e.CompanyId<>p.CompanyId
)
    THROW 51109, 'A Leave balance has a missing map or mismatched company.', 1;

BEGIN TRANSACTION;
IF OBJECT_ID(N'migration.LeavePolicyMap',N'U') IS NULL
    CREATE TABLE migration.LeavePolicyMap (
        LegacyPolicyId int NOT NULL PRIMARY KEY,
        PolicyId int NOT NULL UNIQUE
    );
IF OBJECT_ID(N'migration.LeaveBalanceMap',N'U') IS NULL
    CREATE TABLE migration.LeaveBalanceMap (
        LegacyBalanceId int NOT NULL PRIMARY KEY,
        BalanceId int NOT NULL UNIQUE
    );

IF EXISTS (
    SELECT 1 FROM migration.LeavePolicyMap m
    LEFT JOIN leave.Policies d ON d.Id=m.PolicyId WHERE d.Id IS NULL
)
    THROW 51110, 'A Leave policy map points to a missing destination row.', 1;
IF EXISTS (
    SELECT 1 FROM migration.LeaveBalanceMap m
    LEFT JOIN leave.Balances d ON d.Id=m.BalanceId WHERE d.Id IS NULL
)
    THROW 51111, 'A Leave balance map points to a missing destination row.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffPolicies p
    JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=p.CompanyId
    JOIN leave.Policies d ON d.CompanyId=cm.CompanyId AND d.Name=p.PolicyName
    LEFT JOIN migration.LeavePolicyMap m ON m.LegacyPolicyId=p.PolicyId
    WHERE m.LegacyPolicyId IS NULL
)
    THROW 51112, 'A destination Leave policy name exists without a map.', 1;

MERGE leave.Policies AS target
USING (
    SELECT p.PolicyId, cm.CompanyId, p.PolicyName, p.Category,
           p.Description, p.AnnualHours, p.Active
    FROM HRMS_BHR.dbo.TimeOffPolicies p
    JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=p.CompanyId
    LEFT JOIN migration.LeavePolicyMap m ON m.LegacyPolicyId=p.PolicyId
    WHERE m.LegacyPolicyId IS NULL
) AS source ON 1=0
WHEN NOT MATCHED THEN
    INSERT (CompanyId,Name,Category,Description,AnnualHours,Active)
    VALUES (source.CompanyId,source.PolicyName,source.Category,
            source.Description,source.AnnualHours,source.Active)
OUTPUT source.PolicyId, inserted.Id
INTO migration.LeavePolicyMap (LegacyPolicyId,PolicyId);

IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffBalances b
    JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=b.EmployeeId
    JOIN migration.LeavePolicyMap pm ON pm.LegacyPolicyId=b.PolicyId
    JOIN HRMS_BHR.dbo.Employees e ON e.EmployeeId=b.EmployeeId
    JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
    JOIN leave.Balances d ON d.CompanyId=cm.CompanyId
        AND d.EmployeeId=em.EmployeeId AND d.PolicyId=pm.PolicyId AND d.Year=b.Year
    LEFT JOIN migration.LeaveBalanceMap m ON m.LegacyBalanceId=b.BalanceId
    WHERE m.LegacyBalanceId IS NULL
)
    THROW 51113, 'A destination Leave balance exists without a map.', 1;

MERGE leave.Balances AS target
USING (
    SELECT b.BalanceId, cm.CompanyId, em.EmployeeId, pm.PolicyId,
           b.Year, b.AccruedHours, b.UsedHours,
           b.LastUpdated AT TIME ZONE 'Iran Standard Time' AS UpdatedAtUtc
    FROM HRMS_BHR.dbo.TimeOffBalances b
    JOIN HRMS_BHR.dbo.Employees e ON e.EmployeeId=b.EmployeeId
    JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
    JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=b.EmployeeId
    JOIN migration.LeavePolicyMap pm ON pm.LegacyPolicyId=b.PolicyId
    LEFT JOIN migration.LeaveBalanceMap m ON m.LegacyBalanceId=b.BalanceId
    WHERE m.LegacyBalanceId IS NULL
) AS source ON 1=0
WHEN NOT MATCHED THEN
    INSERT (CompanyId,EmployeeId,PolicyId,Year,AccruedHours,UsedHours,UpdatedAtUtc)
    VALUES (source.CompanyId,source.EmployeeId,source.PolicyId,source.Year,
            source.AccruedHours,source.UsedHours,source.UpdatedAtUtc)
OUTPUT source.BalanceId, inserted.Id
INTO migration.LeaveBalanceMap (LegacyBalanceId,BalanceId);

IF (SELECT COUNT(*) FROM migration.LeavePolicyMap) <>
   (SELECT COUNT(*) FROM HRMS_BHR.dbo.TimeOffPolicies)
    THROW 51114, 'Leave policy import count differs from source.', 1;
IF (SELECT COUNT(*) FROM migration.LeaveBalanceMap) <>
   (SELECT COUNT(*) FROM HRMS_BHR.dbo.TimeOffBalances)
    THROW 51115, 'Leave balance import count differs from source.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffPolicies p
    JOIN migration.LeavePolicyMap m ON m.LegacyPolicyId=p.PolicyId
    JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=p.CompanyId
    JOIN leave.Policies d ON d.Id=m.PolicyId
    WHERE d.CompanyId<>cm.CompanyId OR d.Name<>p.PolicyName
       OR ISNULL(d.Category,N'')<>ISNULL(p.Category,N'')
       OR ISNULL(d.Description,N'')<>ISNULL(p.Description,N'')
       OR d.AnnualHours<>p.AnnualHours OR d.Active<>p.Active
)
    THROW 51116, 'Imported Leave policy values differ from source.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffBalances b
    JOIN migration.LeaveBalanceMap m ON m.LegacyBalanceId=b.BalanceId
    JOIN migration.LeavePolicyMap pm ON pm.LegacyPolicyId=b.PolicyId
    JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=b.EmployeeId
    JOIN HRMS_BHR.dbo.Employees e ON e.EmployeeId=b.EmployeeId
    JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
    JOIN leave.Balances d ON d.Id=m.BalanceId
    WHERE d.CompanyId<>cm.CompanyId OR d.EmployeeId<>em.EmployeeId
       OR d.PolicyId<>pm.PolicyId OR d.Year<>b.Year
       OR d.AccruedHours<>b.AccruedHours OR d.UsedHours<>b.UsedHours
       OR d.UpdatedAtUtc<>b.LastUpdated AT TIME ZONE 'Iran Standard Time'
)
    THROW 51117, 'Imported Leave balance values differ from source.', 1;
COMMIT TRANSACTION;

SELECT N'Policies' AS Entity,
       (SELECT COUNT(*) FROM HRMS_BHR.dbo.TimeOffPolicies) AS SourceCount,
       (SELECT COUNT(*) FROM migration.LeavePolicyMap) AS MappedCount,
       (SELECT COUNT(*) FROM leave.Policies) AS DestinationCount
UNION ALL
SELECT N'Balances',
       (SELECT COUNT(*) FROM HRMS_BHR.dbo.TimeOffBalances),
       (SELECT COUNT(*) FROM migration.LeaveBalanceMap),
       (SELECT COUNT(*) FROM leave.Balances);
