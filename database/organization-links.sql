BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111033616_OrganizationLinks'
)
BEGIN
    CREATE TABLE [organization].[OrganizationLinks] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [Title] nvarchar(100) NOT NULL,
        [Url] nvarchar(2000) NOT NULL,
        [Description] nvarchar(300) NULL,
        [SortOrder] int NOT NULL,
        [Active] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_OrganizationLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OrganizationLinks_SortOrder] CHECK ([SortOrder] >= 0 AND [SortOrder] <= 9999),
        CONSTRAINT [FK_OrganizationLinks_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [organization].[Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111033616_OrganizationLinks'
)
BEGIN
    CREATE INDEX [IX_OrganizationLinks_CompanyId_Active_SortOrder] ON [organization].[OrganizationLinks] ([CompanyId], [Active], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111033616_OrganizationLinks'
)
BEGIN
    INSERT INTO [organization].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111033616_OrganizationLinks', N'10.0.12');
END;

COMMIT;
GO
