-- Trial-only repeatable Training import; HRMS_BHR remains read only.
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
IF DB_NAME() NOT LIKE N'HRMS[_]Next[_]Trial[_]%'
    THROW 51300, 'Destination must be an HRMS_Next_Trial_ database.', 1;
IF DB_ID(N'HRMS_BHR') IS NULL
    THROW 51301, 'Legacy source database was not found.', 1;
IF OBJECT_ID(N'migration.CompanyMap',N'U') IS NULL OR OBJECT_ID(N'migration.EmployeeMap',N'U') IS NULL
    THROW 51302, 'Run the core import first.', 1;
IF COL_LENGTH(N'training.Assignments',N'AssignedByEmployeeId') IS NULL
    THROW 51303, 'Apply Training migrations first.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.TrainingCourses
           WHERE LTRIM(RTRIM(Title))=N'' OR RenewalDays NOT BETWEEN 1 AND 3650 AND RenewalDays IS NOT NULL)
    THROW 51304, 'A legacy Training course cannot be represented safely.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.TrainingCourses GROUP BY Title HAVING COUNT(*)>1)
    THROW 51305, 'Duplicate legacy Training course titles.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TrainingAssignments a
    WHERE CONVERT(time,a.AssignedDate)<>'00:00:00'
       OR (a.DueDate IS NOT NULL AND CONVERT(time,a.DueDate)<>'00:00:00')
       OR (a.CompletedDate IS NOT NULL AND CONVERT(time,a.CompletedDate)<>'00:00:00')
       OR a.DueDate<a.AssignedDate OR a.CompletedDate<a.AssignedDate
)
    THROW 51306, 'A Training assignment date would lose time or has invalid order.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TrainingAssignments a
    LEFT JOIN HRMS_BHR.dbo.Employees e ON e.EmployeeId=a.EmployeeId
    LEFT JOIN HRMS_BHR.dbo.Employees actor ON actor.EmployeeId=a.AssignedBy
    LEFT JOIN HRMS_BHR.dbo.TrainingCourses c ON c.CourseId=a.CourseId
    LEFT JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
    LEFT JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=a.EmployeeId
    LEFT JOIN migration.EmployeeMap am ON am.LegacyEmployeeId=a.AssignedBy
    WHERE e.EmployeeId IS NULL OR c.CourseId IS NULL OR cm.CompanyId IS NULL OR em.EmployeeId IS NULL
       OR (a.AssignedBy IS NOT NULL AND (actor.EmployeeId IS NULL OR actor.CompanyId<>e.CompanyId OR am.EmployeeId IS NULL))
)
    THROW 51307, 'A Training assignment has missing maps or a cross-company actor.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TrainingAssignments
    WHERE CompletedDate IS NULL
    GROUP BY EmployeeId,CourseId HAVING COUNT(*)>1
)
    THROW 51308, 'Duplicate open legacy Training assignments.', 1;

BEGIN TRANSACTION;
IF OBJECT_ID(N'migration.TrainingCourseMap',N'U') IS NULL
    CREATE TABLE migration.TrainingCourseMap (
        LegacyCompanyId int NOT NULL,
        LegacyCourseId int NOT NULL,
        CourseId int NOT NULL UNIQUE,
        CONSTRAINT PK_TrainingCourseMap PRIMARY KEY (LegacyCompanyId,LegacyCourseId)
    );
IF OBJECT_ID(N'migration.TrainingAssignmentMap',N'U') IS NULL
    CREATE TABLE migration.TrainingAssignmentMap (
        LegacyAssignmentId int NOT NULL PRIMARY KEY,
        AssignmentId int NOT NULL UNIQUE
    );
IF EXISTS (SELECT 1 FROM migration.TrainingCourseMap m LEFT JOIN training.Courses d ON d.Id=m.CourseId WHERE d.Id IS NULL)
    THROW 51309, 'A Training course map points to a missing destination row.', 1;
IF EXISTS (SELECT 1 FROM migration.TrainingAssignmentMap m LEFT JOIN training.Assignments d ON d.Id=m.AssignmentId WHERE d.Id IS NULL)
    THROW 51310, 'A Training assignment map points to a missing destination row.', 1;
IF EXISTS (
    SELECT 1 FROM migration.CompanyMap cm CROSS JOIN HRMS_BHR.dbo.TrainingCourses c
    JOIN training.Courses d ON d.CompanyId=cm.CompanyId AND d.Title=c.Title
    LEFT JOIN migration.TrainingCourseMap m ON m.LegacyCompanyId=cm.LegacyCompanyId AND m.LegacyCourseId=c.CourseId
    WHERE m.CourseId IS NULL OR m.CourseId<>d.Id
)
    THROW 51311, 'A destination Training course title conflicts with legacy data.', 1;

MERGE training.Courses AS target
USING (
    SELECT cm.LegacyCompanyId,c.CourseId AS LegacyCourseId,cm.CompanyId,
           c.Title,c.Category,c.Provider,c.Description,c.RenewalDays,
           c.MandatoryForAll,c.Active,
           c.CreatedDate AT TIME ZONE 'Iran Standard Time' AS CreatedAtUtc
    FROM migration.CompanyMap cm CROSS JOIN HRMS_BHR.dbo.TrainingCourses c
    LEFT JOIN migration.TrainingCourseMap m
      ON m.LegacyCompanyId=cm.LegacyCompanyId AND m.LegacyCourseId=c.CourseId
    WHERE m.CourseId IS NULL
) AS source ON 1=0
WHEN NOT MATCHED THEN
    INSERT (CompanyId,Title,Category,Provider,Description,RenewalDays,MandatoryForAll,Active,CreatedAtUtc)
    VALUES (source.CompanyId,source.Title,source.Category,source.Provider,source.Description,
            source.RenewalDays,source.MandatoryForAll,source.Active,source.CreatedAtUtc)
OUTPUT source.LegacyCompanyId,source.LegacyCourseId,inserted.Id
INTO migration.TrainingCourseMap (LegacyCompanyId,LegacyCourseId,CourseId);

MERGE training.Assignments AS target
USING (
    SELECT a.AssignmentId AS LegacyAssignmentId,cm.CompanyId,em.EmployeeId,course.CourseId,
           CONVERT(date,a.AssignedDate) AS AssignedDate,
           CONVERT(date,a.DueDate) AS DueDate,
           CONVERT(date,a.CompletedDate) AS CompletedDate,
           a.CertificateNumber,a.Notes,am.EmployeeId AS AssignedByEmployeeId
    FROM HRMS_BHR.dbo.TrainingAssignments a
    JOIN HRMS_BHR.dbo.Employees e ON e.EmployeeId=a.EmployeeId
    JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
    JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=a.EmployeeId
    JOIN migration.TrainingCourseMap course
      ON course.LegacyCompanyId=e.CompanyId AND course.LegacyCourseId=a.CourseId
    LEFT JOIN migration.EmployeeMap am ON am.LegacyEmployeeId=a.AssignedBy
    LEFT JOIN migration.TrainingAssignmentMap m ON m.LegacyAssignmentId=a.AssignmentId
    WHERE m.LegacyAssignmentId IS NULL
) AS source ON 1=0
WHEN NOT MATCHED THEN
    INSERT (CompanyId,EmployeeId,CourseId,AssignedDate,DueDate,CompletedDate,
            CertificateNumber,Notes,AssignedByUserId,AssignedByEmployeeId)
    VALUES (source.CompanyId,source.EmployeeId,source.CourseId,source.AssignedDate,
            source.DueDate,source.CompletedDate,source.CertificateNumber,source.Notes,
            NULL,source.AssignedByEmployeeId)
OUTPUT source.LegacyAssignmentId,inserted.Id
INTO migration.TrainingAssignmentMap (LegacyAssignmentId,AssignmentId);

IF (SELECT COUNT(*) FROM migration.TrainingCourseMap) <>
   (SELECT COUNT(*) FROM migration.CompanyMap)*(SELECT COUNT(*) FROM HRMS_BHR.dbo.TrainingCourses)
    THROW 51312, 'Training course import count differs from expected company copies.', 1;
IF (SELECT COUNT(*) FROM migration.TrainingAssignmentMap) <>
   (SELECT COUNT(*) FROM HRMS_BHR.dbo.TrainingAssignments)
    THROW 51313, 'Training assignment import count differs from source.', 1;
IF EXISTS (
    SELECT 1 FROM migration.CompanyMap cm CROSS JOIN HRMS_BHR.dbo.TrainingCourses c
    LEFT JOIN migration.TrainingCourseMap m
      ON m.LegacyCompanyId=cm.LegacyCompanyId AND m.LegacyCourseId=c.CourseId
    LEFT JOIN training.Courses d ON d.Id=m.CourseId
    WHERE d.Id IS NULL OR d.CompanyId<>cm.CompanyId OR d.Title<>c.Title
       OR ISNULL(d.Category,N'')<>ISNULL(c.Category,N'')
       OR ISNULL(d.Provider,N'')<>ISNULL(c.Provider,N'')
       OR ISNULL(d.Description,N'')<>ISNULL(c.Description,N'')
       OR ISNULL(d.RenewalDays,-1)<>ISNULL(c.RenewalDays,-1)
       OR d.MandatoryForAll<>c.MandatoryForAll OR d.Active<>c.Active
       OR d.CreatedAtUtc<>c.CreatedDate AT TIME ZONE 'Iran Standard Time'
)
    THROW 51314, 'Imported Training course values differ from source.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.TrainingAssignments a
    JOIN HRMS_BHR.dbo.Employees e ON e.EmployeeId=a.EmployeeId
    JOIN migration.CompanyMap cm ON cm.LegacyCompanyId=e.CompanyId
    JOIN migration.EmployeeMap em ON em.LegacyEmployeeId=a.EmployeeId
    LEFT JOIN migration.EmployeeMap am ON am.LegacyEmployeeId=a.AssignedBy
    JOIN migration.TrainingCourseMap course
      ON course.LegacyCompanyId=e.CompanyId AND course.LegacyCourseId=a.CourseId
    LEFT JOIN migration.TrainingAssignmentMap m ON m.LegacyAssignmentId=a.AssignmentId
    LEFT JOIN training.Assignments d ON d.Id=m.AssignmentId
    WHERE d.Id IS NULL OR d.CompanyId<>cm.CompanyId OR d.EmployeeId<>em.EmployeeId
       OR d.CourseId<>course.CourseId OR d.AssignedDate<>CONVERT(date,a.AssignedDate)
       OR ISNULL(d.DueDate,CONVERT(date,'19000101'))<>ISNULL(CONVERT(date,a.DueDate),CONVERT(date,'19000101'))
       OR ISNULL(d.CompletedDate,CONVERT(date,'19000101'))<>ISNULL(CONVERT(date,a.CompletedDate),CONVERT(date,'19000101'))
       OR ISNULL(d.CertificateNumber,N'')<>ISNULL(a.CertificateNumber,N'')
       OR ISNULL(d.Notes,N'')<>ISNULL(a.Notes,N'')
       OR d.AssignedByUserId IS NOT NULL
       OR ISNULL(d.AssignedByEmployeeId,-1)<>ISNULL(am.EmployeeId,-1)
)
    THROW 51315, 'Imported Training assignment values differ from source.', 1;
COMMIT TRANSACTION;
SELECT (SELECT COUNT(*) FROM HRMS_BHR.dbo.TrainingCourses) AS SourceCourses,
       (SELECT COUNT(*) FROM migration.TrainingCourseMap) AS MappedCourses,
       (SELECT COUNT(*) FROM HRMS_BHR.dbo.TrainingAssignments) AS SourceAssignments,
       (SELECT COUNT(*) FROM migration.TrainingAssignmentMap) AS MappedAssignments;
