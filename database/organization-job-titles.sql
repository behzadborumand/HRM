BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110201728_OrganizationJobTitles'
)
BEGIN
    CREATE TABLE [organization].[JobTitles] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_JobTitles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JobTitles_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [organization].[Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110201728_OrganizationJobTitles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JobTitles_CompanyId_Name] ON [organization].[JobTitles] ([CompanyId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110201728_OrganizationJobTitles'
)
BEGIN
    INSERT INTO [organization].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110201728_OrganizationJobTitles', N'10.0.12');
END;

COMMIT;
GO
