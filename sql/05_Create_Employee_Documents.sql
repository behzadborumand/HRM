USE HRMS_BHR;
GO

-- مدارک و اسناد کارکنان (فاز ۱۱)
IF OBJECT_ID('dbo.EmployeeDocuments', 'U') IS NULL
CREATE TABLE dbo.EmployeeDocuments
(
    DocumentId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EmployeeId     INT NOT NULL CONSTRAINT FK_EmployeeDocuments_Employees REFERENCES dbo.Employees(EmployeeId),
    Title          NVARCHAR(200) NOT NULL,
    Category       NVARCHAR(50) NULL,
    FileName       NVARCHAR(260) NOT NULL,
    StoredPath     NVARCHAR(400) NOT NULL,
    ContentType    NVARCHAR(120) NULL,
    FileSize       BIGINT NOT NULL CONSTRAINT DF_EmployeeDocuments_FileSize DEFAULT 0,
    UploadedBy     INT NULL,
    UploadDate     DATETIME NOT NULL CONSTRAINT DF_EmployeeDocuments_UploadDate DEFAULT GETDATE(),
    ExpiryDate     DATETIME NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeDocuments_EmployeeId'
               AND object_id = OBJECT_ID('dbo.EmployeeDocuments'))
    CREATE INDEX IX_EmployeeDocuments_EmployeeId
        ON dbo.EmployeeDocuments(EmployeeId, UploadDate DESC);
GO
