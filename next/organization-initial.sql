IF OBJECT_ID(N'[organization].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'organization') IS NULL EXEC(N'CREATE SCHEMA [organization];');
    CREATE TABLE [organization].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201134_OrganizationInitial'
)
BEGIN
    IF SCHEMA_ID(N'organization') IS NULL EXEC(N'CREATE SCHEMA [organization];');
END;

IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201134_OrganizationInitial'
)
BEGIN
    CREATE TABLE [organization].[Companies] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201134_OrganizationInitial'
)
BEGIN
    CREATE TABLE [organization].[Departments] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [ParentId] int NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201134_OrganizationInitial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Departments_CompanyId_Name] ON [organization].[Departments] ([CompanyId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201134_OrganizationInitial'
)
BEGIN
    INSERT INTO [organization].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107201134_OrganizationInitial', N'9.0.18');
END;

COMMIT;
GO
