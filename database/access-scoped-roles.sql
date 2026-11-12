SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'HRMS_Next_Trial_20261108' THROW 50000, 'Scoped roles script is restricted to the disposable trial database.', 1;
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112040306_ScopedRoles'
)
BEGIN
    ALTER TABLE [access].[Users] ADD [RoleId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112040306_ScopedRoles'
)
BEGIN
    CREATE TABLE [access].[Roles] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Active] bit NOT NULL,
        [CanReadEmployees] bit NOT NULL,
        [CanWriteEmployees] bit NOT NULL,
        [CanManageTeam] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Roles_Id_CompanyId] UNIQUE ([Id], [CompanyId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112040306_ScopedRoles'
)
BEGIN
    CREATE TABLE [access].[RoleAudits] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [RoleId] int NOT NULL,
        [ActorUserId] int NOT NULL,
        [AtUtc] datetimeoffset NOT NULL,
        [Action] nvarchar(30) NOT NULL,
        [Summary] nvarchar(1000) NOT NULL,
        CONSTRAINT [PK_RoleAudits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoleAudits_Roles_RoleId_CompanyId] FOREIGN KEY ([RoleId], [CompanyId]) REFERENCES [access].[Roles] ([Id], [CompanyId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112040306_ScopedRoles'
)
BEGIN
    CREATE INDEX [IX_Users_RoleId_CompanyId] ON [access].[Users] ([RoleId], [CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112040306_ScopedRoles'
)
BEGIN
    CREATE INDEX [IX_RoleAudits_CompanyId_RoleId_AtUtc] ON [access].[RoleAudits] ([CompanyId], [RoleId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112040306_ScopedRoles'
)
BEGIN
    CREATE INDEX [IX_RoleAudits_RoleId_CompanyId] ON [access].[RoleAudits] ([RoleId], [CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112040306_ScopedRoles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_CompanyId_Name] ON [access].[Roles] ([CompanyId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112040306_ScopedRoles'
)
BEGIN
    ALTER TABLE [access].[Users] ADD CONSTRAINT [FK_Users_Roles_RoleId_CompanyId] FOREIGN KEY ([RoleId], [CompanyId]) REFERENCES [access].[Roles] ([Id], [CompanyId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112040306_ScopedRoles'
)
BEGIN
    INSERT INTO [access].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112040306_ScopedRoles', N'10.0.12');
END;

COMMIT;
GO
