SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;
GO
IF OBJECT_ID(N'[training].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'training') IS NULL EXEC(N'CREATE SCHEMA [training];');
    CREATE TABLE [training].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [training].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109184154_TrainingInitial'
)
BEGIN
    IF SCHEMA_ID(N'training') IS NULL EXEC(N'CREATE SCHEMA [training];');
END;

IF NOT EXISTS (
    SELECT * FROM [training].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109184154_TrainingInitial'
)
BEGIN
    CREATE TABLE [training].[Courses] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Category] nvarchar(50) NULL,
        [Provider] nvarchar(200) NULL,
        [Description] nvarchar(1000) NULL,
        [RenewalDays] int NULL,
        [MandatoryForAll] bit NOT NULL,
        [Active] bit NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Courses] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Courses_Id_CompanyId] UNIQUE ([Id], [CompanyId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [training].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109184154_TrainingInitial'
)
BEGIN
    CREATE TABLE [training].[Assignments] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [CourseId] int NOT NULL,
        [AssignedDate] date NOT NULL,
        [DueDate] date NULL,
        [CompletedDate] date NULL,
        [CertificateNumber] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [AssignedByUserId] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Assignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Assignments_Courses_CourseId_CompanyId] FOREIGN KEY ([CourseId], [CompanyId]) REFERENCES [training].[Courses] ([Id], [CompanyId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [training].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109184154_TrainingInitial'
)
BEGIN
    CREATE INDEX [IX_Assignments_CompanyId_EmployeeId_DueDate] ON [training].[Assignments] ([CompanyId], [EmployeeId], [DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [training].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109184154_TrainingInitial'
)
BEGIN
    CREATE INDEX [IX_Assignments_CourseId_CompanyId] ON [training].[Assignments] ([CourseId], [CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [training].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109184154_TrainingInitial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Courses_CompanyId_Title] ON [training].[Courses] ([CompanyId], [Title]);
END;

IF NOT EXISTS (
    SELECT * FROM [training].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109184154_TrainingInitial'
)
BEGIN
    INSERT INTO [training].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109184154_TrainingInitial', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [training].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109185018_TrainingOpenAssignmentUnique'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Assignments_CompanyId_EmployeeId_CourseId] ON [training].[Assignments] ([CompanyId], [EmployeeId], [CourseId]) WHERE [CompletedDate] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [training].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109185018_TrainingOpenAssignmentUnique'
)
BEGIN
    INSERT INTO [training].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109185018_TrainingOpenAssignmentUnique', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [training].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109200625_TrainingActorProvenance'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[training].[Assignments]') AND [c].[name] = N'AssignedByUserId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [training].[Assignments] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [training].[Assignments] ALTER COLUMN [AssignedByUserId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [training].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109200625_TrainingActorProvenance'
)
BEGIN
    ALTER TABLE [training].[Assignments] ADD [AssignedByEmployeeId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [training].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109200625_TrainingActorProvenance'
)
BEGIN
    INSERT INTO [training].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109200625_TrainingActorProvenance', N'10.0.12');
END;

COMMIT;
GO
