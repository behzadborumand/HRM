USE HRMS_BHR;
GO

IF OBJECT_ID('dbo.OrganizationLinks', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrganizationLinks
    (
        LinkId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Title       NVARCHAR(100) NOT NULL,
        Url         NVARCHAR(2000) NOT NULL,
        Description NVARCHAR(300) NULL,
        SortOrder   INT NOT NULL CONSTRAINT DF_OrganizationLinks_SortOrder DEFAULT 0,
        Active      BIT NOT NULL CONSTRAINT DF_OrganizationLinks_Active DEFAULT 1
    );
END;
GO
