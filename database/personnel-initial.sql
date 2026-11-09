IF OBJECT_ID(N'[personnel].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'personnel') IS NULL EXEC(N'CREATE SCHEMA [personnel];');
    CREATE TABLE [personnel].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201140_PersonnelInitial'
)
BEGIN
    IF SCHEMA_ID(N'personnel') IS NULL EXEC(N'CREATE SCHEMA [personnel];');
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201140_PersonnelInitial'
)
BEGIN
    CREATE TABLE [personnel].[EmployeeAudits] (
        [Id] bigint NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [ActorUserId] int NOT NULL,
        [AtUtc] datetimeoffset NOT NULL,
        [Action] nvarchar(30) NOT NULL,
        [Summary] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_EmployeeAudits] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201140_PersonnelInitial'
)
BEGIN
    CREATE TABLE [personnel].[Employees] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [DepartmentId] int NOT NULL,
        [EmployeeNumber] nvarchar(50) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Email] nvarchar(256) NULL,
        [HireDate] date NOT NULL,
        [TerminationDate] date NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201140_PersonnelInitial'
)
BEGIN
    CREATE INDEX [IX_EmployeeAudits_EmployeeId_AtUtc] ON [personnel].[EmployeeAudits] ([EmployeeId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201140_PersonnelInitial'
)
BEGIN
    CREATE INDEX [IX_Employees_CompanyId_DepartmentId_LastName] ON [personnel].[Employees] ([CompanyId], [DepartmentId], [LastName]);
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201140_PersonnelInitial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Employees_CompanyId_EmployeeNumber] ON [personnel].[Employees] ([CompanyId], [EmployeeNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201140_PersonnelInitial'
)
BEGIN
    INSERT INTO [personnel].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107201140_PersonnelInitial', N'10.0.12');
END;

COMMIT;
GO
