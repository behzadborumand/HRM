BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182045_PersonnelProfilePhotos'
)
BEGIN
    CREATE TABLE [personnel].[EmployeePhotos] (
        [EmployeeId] int NOT NULL,
        [CompanyId] int NOT NULL,
        [Bytes] varbinary(max) NOT NULL,
        [ContentType] nvarchar(50) NOT NULL,
        [Sha256] nvarchar(64) NOT NULL,
        [UploadedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EmployeePhotos] PRIMARY KEY ([EmployeeId]),
        CONSTRAINT [FK_EmployeePhotos_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [personnel].[Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182045_PersonnelProfilePhotos'
)
BEGIN
    CREATE INDEX [IX_Employees_CompanyId_LastName_FirstName_Id] ON [personnel].[Employees] ([CompanyId], [LastName], [FirstName], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182045_PersonnelProfilePhotos'
)
BEGIN
    CREATE INDEX [IX_EmployeePhotos_CompanyId] ON [personnel].[EmployeePhotos] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111182045_PersonnelProfilePhotos'
)
BEGIN
    INSERT INTO [personnel].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111182045_PersonnelProfilePhotos', N'10.0.12');
END;

COMMIT;
GO
