-- Trial-only, repeatable Leave request transfer. Requires core and Leave foundation maps.
-- Legacy source HRMS_BHR is read only; actor employee IDs are preserved without invented users.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT LIKE N'HRMS[_]Next[_]Trial[_]%'
    THROW 51200, 'Destination must be an HRMS_Next_Trial_ database.', 1;
IF DB_ID(N'HRMS_BHR') IS NULL
    THROW 51201, 'Legacy source database was not found.', 1;
IF OBJECT_ID(N'migration.EmployeeMap',N'U') IS NULL OR OBJECT_ID(N'migration.LeavePolicyMap',N'U') IS NULL
    THROW 51202, 'Run core and Leave foundation imports first.', 1;
IF COL_LENGTH(N'leave.Requests',N'RequestedByEmployeeId') IS NULL
    THROW 51203, 'Apply the Leave actor provenance migration first.', 1;

IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffRequests r
    WHERE r.StartDate IS NULL OR r.EndDate IS NULL OR r.EndDate<r.StartDate
       OR r.TotalHours IS NULL OR r.TotalHours<=0 OR r.TotalHours>99999999
       OR r.RequestedDate IS NULL OR r.Status IS NULL
       OR r.Status NOT IN (N'Pending',N'Approved',N'Rejected',N'Cancelled')
       OR LEN(r.Reason)>1000 OR LEN(r.Comments)>1000
)
    THROW 51204, 'Legacy Leave request fields cannot be represented safely.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffRequests r
    WHERE (r.Status IN (N'Approved',N'Rejected') AND (r.ApprovedBy IS NULL OR r.ApprovedDate IS NULL))
       OR (r.Status=N'Pending' AND (r.ApprovedBy IS NOT NULL OR r.ApprovedDate IS NOT NULL))
)
    THROW 51205, 'Legacy Leave request decision fields are inconsistent.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffRequests r
    LEFT JOIN HRMS_BHR.dbo.Employees e ON e.EmployeeId=r.EmployeeId
    LEFT JOIN HRMS_BHR.dbo.TimeOffPolicies p ON p.PolicyId=r.PolicyId
    LEFT JOIN HRMS_BHR.dbo.Employees requester ON requester.EmployeeId=r.RequestedBy
    LEFT JOIN HRMS_BHR.dbo.Employees approver ON approver.EmployeeId=r.ApprovedBy
    LEFT JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
    LEFT JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=r.EmployeeId
    LEFT JOIN migration.EmployeeMap rm ON rm.LegacyEmployeeId=r.RequestedBy
    LEFT JOIN migration.EmployeeMap am ON am.LegacyEmployeeId=r.ApprovedBy
    LEFT JOIN migration.LeavePolicyMap pm ON pm.LegacyPolicyId=r.PolicyId
    WHERE e.EmployeeId IS NULL OR p.PolicyId IS NULL OR requester.EmployeeId IS NULL
       OR (r.ApprovedBy IS NOT NULL AND approver.EmployeeId IS NULL)
       OR p.CompanyId<>e.CompanyId OR requester.CompanyId<>e.CompanyId
       OR (r.ApprovedBy IS NOT NULL AND approver.CompanyId<>e.CompanyId)
       OR cm.CompanyId IS NULL OR em.EmployeeId IS NULL OR rm.EmployeeId IS NULL
       OR pm.PolicyId IS NULL OR (r.ApprovedBy IS NOT NULL AND am.EmployeeId IS NULL)
)
    THROW 51206, 'A Leave request has missing maps or cross-company actors.', 1;

BEGIN TRANSACTION;
IF OBJECT_ID(N'migration.LeaveRequestMap',N'U') IS NULL
    CREATE TABLE migration.LeaveRequestMap (
        LegacyRequestId int NOT NULL PRIMARY KEY,
        RequestId int NOT NULL UNIQUE
    );
IF EXISTS (
    SELECT 1 FROM migration.LeaveRequestMap m
    LEFT JOIN leave.Requests d ON d.Id=m.RequestId WHERE d.Id IS NULL
)
    THROW 51207, 'A Leave request map points to a missing destination row.', 1;

MERGE leave.Requests AS target
USING (
    SELECT r.RequestId AS LegacyRequestId, cm.CompanyId, em.EmployeeId,
           pm.PolicyId, r.StartDate, r.EndDate, r.TotalHours,
           r.Reason, r.Status, rm.EmployeeId AS RequestedByEmployeeId,
           r.RequestedDate AT TIME ZONE 'Iran Standard Time' AS RequestedAtUtc,
           am.EmployeeId AS DecidedByEmployeeId,
           r.ApprovedDate AT TIME ZONE 'Iran Standard Time' AS DecidedAtUtc,
           r.Comments
    FROM HRMS_BHR.dbo.TimeOffRequests r
    JOIN HRMS_BHR.dbo.Employees e ON e.EmployeeId=r.EmployeeId
    JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
    JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=r.EmployeeId
    JOIN migration.EmployeeMap rm ON rm.LegacyEmployeeId=r.RequestedBy
    LEFT JOIN migration.EmployeeMap am ON am.LegacyEmployeeId=r.ApprovedBy
    JOIN migration.LeavePolicyMap pm ON pm.LegacyPolicyId=r.PolicyId
    LEFT JOIN migration.LeaveRequestMap map ON map.LegacyRequestId=r.RequestId
    WHERE map.LegacyRequestId IS NULL
) AS source ON 1=0
WHEN NOT MATCHED THEN
    INSERT (CompanyId,EmployeeId,PolicyId,StartDate,EndDate,Hours,Reason,Status,
            RequestedByUserId,RequestedByEmployeeId,RequestedAtUtc,
            DecidedByUserId,DecidedByEmployeeId,DecidedAtUtc,Comments)
    VALUES (source.CompanyId,source.EmployeeId,source.PolicyId,source.StartDate,
            source.EndDate,source.TotalHours,source.Reason,source.Status,
            NULL,source.RequestedByEmployeeId,source.RequestedAtUtc,
            NULL,source.DecidedByEmployeeId,source.DecidedAtUtc,source.Comments)
OUTPUT source.LegacyRequestId,inserted.Id
INTO migration.LeaveRequestMap (LegacyRequestId,RequestId);

IF (SELECT COUNT(*) FROM migration.LeaveRequestMap) <>
   (SELECT COUNT(*) FROM HRMS_BHR.dbo.TimeOffRequests)
    THROW 51208, 'Leave request import count differs from source.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TimeOffRequests r
    JOIN migration.LeaveRequestMap map ON map.LegacyRequestId=r.RequestId
    JOIN leave.Requests d ON d.Id=map.RequestId
    JOIN HRMS_BHR.dbo.Employees e ON e.EmployeeId=r.EmployeeId
    JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
    JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=r.EmployeeId
    JOIN migration.EmployeeMap rm ON rm.LegacyEmployeeId=r.RequestedBy
    LEFT JOIN migration.EmployeeMap am ON am.LegacyEmployeeId=r.ApprovedBy
    JOIN migration.LeavePolicyMap pm ON pm.LegacyPolicyId=r.PolicyId
    WHERE d.CompanyId<>cm.CompanyId OR d.EmployeeId<>em.EmployeeId
       OR d.PolicyId<>pm.PolicyId OR d.StartDate<>r.StartDate OR d.EndDate<>r.EndDate
       OR d.Hours<>r.TotalHours OR d.Status<>r.Status
       OR ISNULL(d.Reason,N'')<>ISNULL(r.Reason,N'')
       OR ISNULL(d.Comments,N'')<>ISNULL(r.Comments,N'')
       OR d.RequestedByUserId IS NOT NULL OR d.DecidedByUserId IS NOT NULL
       OR d.RequestedByEmployeeId<>rm.EmployeeId
       OR ISNULL(d.DecidedByEmployeeId,-1)<>ISNULL(am.EmployeeId,-1)
       OR d.RequestedAtUtc<>r.RequestedDate AT TIME ZONE 'Iran Standard Time'
       OR ISNULL(d.DecidedAtUtc,CONVERT(datetimeoffset,'1900-01-01T00:00:00+00:00')) <>
          ISNULL(r.ApprovedDate AT TIME ZONE 'Iran Standard Time',
                 CONVERT(datetimeoffset,'1900-01-01T00:00:00+00:00'))
)
    THROW 51209, 'Imported Leave request values differ from source.', 1;
COMMIT TRANSACTION;

SELECT (SELECT COUNT(*) FROM HRMS_BHR.dbo.TimeOffRequests) AS SourceCount,
       (SELECT COUNT(*) FROM migration.LeaveRequestMap) AS MappedCount,
       (SELECT COUNT(*) FROM leave.Requests) AS DestinationCount;
SELECT Status,COUNT(*) AS Requests
FROM leave.Requests
WHERE Id IN (SELECT RequestId FROM migration.LeaveRequestMap)
GROUP BY Status ORDER BY Status;
