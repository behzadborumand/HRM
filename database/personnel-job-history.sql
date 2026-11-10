SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110181542_PersonnelJobHistory'
)
BEGIN
    CREATE TABLE [personnel].[EmployeeJobs] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [DepartmentId] int NULL,
        [ManagerEmployeeId] int NULL,
        [JobTitle] nvarchar(200) NULL,
        [EmploymentType] nvarchar(50) NULL,
        [PayType] nvarchar(50) NULL,
        [PayRate] decimal(18,2) NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [IsCurrent] bit NOT NULL,
        CONSTRAINT [PK_EmployeeJobs] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EmployeeJobs_DateRange] CHECK ([EndDate] IS NULL OR [EndDate] >= [StartDate]),
        CONSTRAINT [FK_EmployeeJobs_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [personnel].[Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeJobs_Employees_ManagerEmployeeId] FOREIGN KEY ([ManagerEmployeeId]) REFERENCES [personnel].[Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110181542_PersonnelJobHistory'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EmployeeJobs_CompanyId_EmployeeId] ON [personnel].[EmployeeJobs] ([CompanyId], [EmployeeId]) WHERE [IsCurrent] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110181542_PersonnelJobHistory'
)
BEGIN
    CREATE INDEX [IX_EmployeeJobs_CompanyId_EmployeeId_StartDate] ON [personnel].[EmployeeJobs] ([CompanyId], [EmployeeId], [StartDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110181542_PersonnelJobHistory'
)
BEGIN
    CREATE INDEX [IX_EmployeeJobs_EmployeeId] ON [personnel].[EmployeeJobs] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110181542_PersonnelJobHistory'
)
BEGIN
    CREATE INDEX [IX_EmployeeJobs_ManagerEmployeeId] ON [personnel].[EmployeeJobs] ([ManagerEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110181542_PersonnelJobHistory'
)
BEGIN
    INSERT INTO [personnel].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110181542_PersonnelJobHistory', N'10.0.12');
END;

COMMIT;
GO
