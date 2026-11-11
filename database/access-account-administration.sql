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
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111041401_AccountAdministration'
)
BEGIN
    ALTER TABLE [access].[Users] ADD [EmployeeId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111041401_AccountAdministration'
)
BEGIN
    ALTER TABLE [access].[Users] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111041401_AccountAdministration'
)
BEGIN
    CREATE TABLE [access].[Audits] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [TargetUserId] int NOT NULL,
        [ActorUserId] int NOT NULL,
        [AtUtc] datetimeoffset NOT NULL,
        [Action] nvarchar(30) NOT NULL,
        [Summary] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Audits] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111041401_AccountAdministration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_CompanyId_EmployeeId] ON [access].[Users] ([CompanyId], [EmployeeId]) WHERE [EmployeeId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111041401_AccountAdministration'
)
BEGIN
    CREATE INDEX [IX_Audits_CompanyId_TargetUserId_AtUtc] ON [access].[Audits] ([CompanyId], [TargetUserId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111041401_AccountAdministration'
)
BEGIN
    INSERT INTO [access].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111041401_AccountAdministration', N'10.0.12');
END;

COMMIT;
GO
