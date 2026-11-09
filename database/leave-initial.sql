IF OBJECT_ID(N'[leave].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'leave') IS NULL EXEC(N'CREATE SCHEMA [leave];');
    CREATE TABLE [leave].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [leave].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108040759_LeaveInitial'
)
BEGIN
    IF SCHEMA_ID(N'leave') IS NULL EXEC(N'CREATE SCHEMA [leave];');
END;

IF NOT EXISTS (
    SELECT * FROM [leave].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108040759_LeaveInitial'
)
BEGIN
    CREATE TABLE [leave].[Policies] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Category] nvarchar(50) NULL,
        [Description] nvarchar(1000) NULL,
        [AnnualHours] decimal(10,2) NOT NULL,
        [Active] bit NOT NULL,
        CONSTRAINT [PK_Policies] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Policies_Id_CompanyId] UNIQUE ([Id], [CompanyId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [leave].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108040759_LeaveInitial'
)
BEGIN
    CREATE TABLE [leave].[Balances] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [PolicyId] int NOT NULL,
        [Year] int NOT NULL,
        [AccruedHours] decimal(10,2) NOT NULL,
        [UsedHours] decimal(10,2) NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Balances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Balances_Policies_PolicyId_CompanyId] FOREIGN KEY ([PolicyId], [CompanyId]) REFERENCES [leave].[Policies] ([Id], [CompanyId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [leave].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108040759_LeaveInitial'
)
BEGIN
    CREATE TABLE [leave].[Requests] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [PolicyId] int NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Hours] decimal(10,2) NOT NULL,
        [Reason] nvarchar(1000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [RequestedByUserId] int NOT NULL,
        [RequestedAtUtc] datetimeoffset NOT NULL,
        [DecidedByUserId] int NULL,
        [DecidedAtUtc] datetimeoffset NULL,
        [Comments] nvarchar(1000) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Requests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Requests_Policies_PolicyId_CompanyId] FOREIGN KEY ([PolicyId], [CompanyId]) REFERENCES [leave].[Policies] ([Id], [CompanyId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [leave].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108040759_LeaveInitial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Balances_CompanyId_EmployeeId_PolicyId_Year] ON [leave].[Balances] ([CompanyId], [EmployeeId], [PolicyId], [Year]);
END;

IF NOT EXISTS (
    SELECT * FROM [leave].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108040759_LeaveInitial'
)
BEGIN
    CREATE INDEX [IX_Balances_PolicyId_CompanyId] ON [leave].[Balances] ([PolicyId], [CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [leave].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108040759_LeaveInitial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Policies_CompanyId_Name] ON [leave].[Policies] ([CompanyId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [leave].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108040759_LeaveInitial'
)
BEGIN
    CREATE INDEX [IX_Requests_CompanyId_EmployeeId_Status_StartDate] ON [leave].[Requests] ([CompanyId], [EmployeeId], [Status], [StartDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [leave].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108040759_LeaveInitial'
)
BEGIN
    CREATE INDEX [IX_Requests_PolicyId_CompanyId] ON [leave].[Requests] ([PolicyId], [CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [leave].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108040759_LeaveInitial'
)
BEGIN
    INSERT INTO [leave].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108040759_LeaveInitial', N'10.0.12');
END;

COMMIT;
GO
