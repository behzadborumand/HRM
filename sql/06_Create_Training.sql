USE HRMS_BHR;
GO

-- آموزش‌های کارکنان (فاز ۱۵)
IF OBJECT_ID('dbo.TrainingCourses', 'U') IS NULL
CREATE TABLE dbo.TrainingCourses
(
    CourseId        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Title           NVARCHAR(200) NOT NULL,
    Category        NVARCHAR(50) NULL,
    Provider        NVARCHAR(200) NULL,
    Description     NVARCHAR(1000) NULL,
    RenewalDays     INT NULL,
    MandatoryForAll BIT NOT NULL CONSTRAINT DF_TrainingCourses_MandatoryForAll DEFAULT 0,
    Active          BIT NOT NULL CONSTRAINT DF_TrainingCourses_Active DEFAULT 1,
    CreatedDate     DATETIME NOT NULL CONSTRAINT DF_TrainingCourses_CreatedDate DEFAULT GETDATE()
);
GO

IF OBJECT_ID('dbo.TrainingAssignments', 'U') IS NULL
CREATE TABLE dbo.TrainingAssignments
(
    AssignmentId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EmployeeId        INT NOT NULL CONSTRAINT FK_TrainingAssignments_Employees REFERENCES dbo.Employees(EmployeeId),
    CourseId          INT NOT NULL CONSTRAINT FK_TrainingAssignments_TrainingCourses REFERENCES dbo.TrainingCourses(CourseId),
    AssignedDate      DATETIME NOT NULL CONSTRAINT DF_TrainingAssignments_AssignedDate DEFAULT GETDATE(),
    DueDate           DATETIME NULL,
    CompletedDate     DATETIME NULL,
    CertificateNumber NVARCHAR(100) NULL,
    Notes             NVARCHAR(500) NULL,
    AssignedBy        INT NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TrainingAssignments_EmployeeId'
               AND object_id = OBJECT_ID('dbo.TrainingAssignments'))
    CREATE INDEX IX_TrainingAssignments_EmployeeId
        ON dbo.TrainingAssignments(EmployeeId, DueDate);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TrainingAssignments_CourseId'
               AND object_id = OBJECT_ID('dbo.TrainingAssignments'))
    CREATE INDEX IX_TrainingAssignments_CourseId
        ON dbo.TrainingAssignments(CourseId);
GO
