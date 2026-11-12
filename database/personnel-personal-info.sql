SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'HRMS_Next_Trial_20261108' THROW 50000, 'Personal info migration is restricted to the disposable trial database.', 1;
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112113038_PersonnelPersonalInfo'
)
BEGIN
    ALTER TABLE [personnel].[Employees] ADD CONSTRAINT [AK_Employees_Id_CompanyId] UNIQUE ([Id], [CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112113038_PersonnelPersonalInfo'
)
BEGIN
    CREATE TABLE [personnel].[PersonalInfos] (
        [EmployeeId] int NOT NULL,
        [CompanyId] int NOT NULL,
        [PreferredName] nvarchar(100) NULL,
        [Phone] nvarchar(50) NULL,
        [DateOfBirth] date NULL,
        [Gender] nvarchar(20) NULL,
        [MaritalStatus] nvarchar(20) NULL,
        [AddressLine1] nvarchar(200) NULL,
        [AddressLine2] nvarchar(200) NULL,
        [City] nvarchar(100) NULL,
        [State] nvarchar(100) NULL,
        [PostalCode] nvarchar(20) NULL,
        [Country] nvarchar(100) NULL,
        [HomePhone] nvarchar(50) NULL,
        [MobilePhone] nvarchar(50) NULL,
        [EmergencyContactName] nvarchar(200) NULL,
        [EmergencyContactPhone] nvarchar(50) NULL,
        [EmergencyContactRelation] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PersonalInfos] PRIMARY KEY ([EmployeeId]),
        CONSTRAINT [FK_PersonalInfos_Employees_EmployeeId_CompanyId] FOREIGN KEY ([EmployeeId], [CompanyId]) REFERENCES [personnel].[Employees] ([Id], [CompanyId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112113038_PersonnelPersonalInfo'
)
BEGIN
    CREATE INDEX [IX_PersonalInfos_EmployeeId_CompanyId] ON [personnel].[PersonalInfos] ([EmployeeId], [CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112113038_PersonnelPersonalInfo'
)
BEGIN
    INSERT INTO [personnel].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112113038_PersonnelPersonalInfo', N'10.0.12');
END;

COMMIT;
GO
