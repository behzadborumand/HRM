IF OBJECT_ID(N'[access].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'access') IS NULL EXEC(N'CREATE SCHEMA [access];');
    CREATE TABLE [access].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201128_AccessInitial'
)
BEGIN
    IF SCHEMA_ID(N'access') IS NULL EXEC(N'CREATE SCHEMA [access];');
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201128_AccessInitial'
)
BEGIN
    CREATE TABLE [access].[Users] (
        [Id] int NOT NULL IDENTITY,
        [Username] nvarchar(100) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [CompanyId] int NOT NULL,
        [IsAdmin] bit NOT NULL,
        [CanReadEmployees] bit NOT NULL,
        [CanWriteEmployees] bit NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201128_AccessInitial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [access].[Users] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [access].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107201128_AccessInitial'
)
BEGIN
    INSERT INTO [access].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107201128_AccessInitial', N'9.0.18');
END;

COMMIT;
GO
