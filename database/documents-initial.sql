IF OBJECT_ID(N'[documents].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'documents') IS NULL EXEC(N'CREATE SCHEMA [documents];');
    CREATE TABLE [documents].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [documents].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109202637_DocumentsInitial'
)
BEGIN
    IF SCHEMA_ID(N'documents') IS NULL EXEC(N'CREATE SCHEMA [documents];');
END;

IF NOT EXISTS (
    SELECT * FROM [documents].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109202637_DocumentsInitial'
)
BEGIN
    CREATE TABLE [documents].[DocumentAudits] (
        [Id] int NOT NULL IDENTITY,
        [DocumentId] int NOT NULL,
        [CompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [ActorUserId] int NOT NULL,
        [Action] nvarchar(20) NOT NULL,
        [AtUtc] datetimeoffset NOT NULL,
        [Sha256] varbinary(32) NOT NULL,
        CONSTRAINT [PK_DocumentAudits] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [documents].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109202637_DocumentsInitial'
)
BEGIN
    CREATE TABLE [documents].[EmployeeDocuments] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Category] nvarchar(50) NULL,
        [FileName] nvarchar(260) NOT NULL,
        [FileSize] bigint NOT NULL,
        [Bytes] varbinary(max) NOT NULL,
        [Sha256] varbinary(32) NOT NULL,
        [UploadedByUserId] int NOT NULL,
        [UploadedAtUtc] datetimeoffset NOT NULL,
        [ExpiryDate] date NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EmployeeDocuments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [documents].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109202637_DocumentsInitial'
)
BEGIN
    CREATE INDEX [IX_DocumentAudits_CompanyId_EmployeeId_AtUtc] ON [documents].[DocumentAudits] ([CompanyId], [EmployeeId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [documents].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109202637_DocumentsInitial'
)
BEGIN
    CREATE INDEX [IX_EmployeeDocuments_CompanyId_EmployeeId_UploadedAtUtc] ON [documents].[EmployeeDocuments] ([CompanyId], [EmployeeId], [UploadedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [documents].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109202637_DocumentsInitial'
)
BEGIN
    INSERT INTO [documents].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109202637_DocumentsInitial', N'10.0.12');
END;

COMMIT;
GO
