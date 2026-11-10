BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110203213_OrganizationDepartmentAdministration'
)
BEGIN
    ALTER TABLE [organization].[Departments] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110203213_OrganizationDepartmentAdministration'
)
BEGIN
    ALTER TABLE [organization].[Departments] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [organization].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110203213_OrganizationDepartmentAdministration'
)
BEGIN
    INSERT INTO [organization].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110203213_OrganizationDepartmentAdministration', N'10.0.12');
END;

COMMIT;
GO
