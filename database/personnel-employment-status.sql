SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'HRMS_Next_Trial_20261108' THROW 50000, 'Employment status migration is restricted to the disposable trial database.', 1;
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113131149_PersonnelEmploymentStatus'
)
BEGIN
    ALTER TABLE [personnel].[Employees] ADD [IsOnLeave] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113131149_PersonnelEmploymentStatus'
)
BEGIN
    CREATE TABLE [personnel].[EmploymentStatusChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [EffectiveDate] date NULL,
        [FromStatus] nvarchar(20) NULL,
        [ToStatus] nvarchar(20) NOT NULL,
        [ActorUserId] int NULL,
        [AtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_EmploymentStatusChanges] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EmploymentStatusChanges_Baseline] CHECK (([EffectiveDate] IS NULL AND [FromStatus] IS NULL AND [ActorUserId] IS NULL) OR ([EffectiveDate] IS NOT NULL AND [FromStatus] IS NOT NULL AND [ActorUserId] IS NOT NULL)),
        CONSTRAINT [CK_EmploymentStatusChanges_Status] CHECK ([ToStatus] IN (N'Active',N'OnLeave') AND ([FromStatus] IS NULL OR ([FromStatus] IN (N'Active',N'OnLeave') AND [FromStatus]<>[ToStatus]))),
        CONSTRAINT [FK_EmploymentStatusChanges_Employees_EmployeeId_CompanyId] FOREIGN KEY ([EmployeeId], [CompanyId]) REFERENCES [personnel].[Employees] ([Id], [CompanyId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113131149_PersonnelEmploymentStatus'
)
BEGIN
    CREATE INDEX [IX_EmploymentStatusChanges_CompanyId_EmployeeId_EffectiveDate_Id] ON [personnel].[EmploymentStatusChanges] ([CompanyId], [EmployeeId], [EffectiveDate], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113131149_PersonnelEmploymentStatus'
)
BEGIN
    CREATE INDEX [IX_EmploymentStatusChanges_EmployeeId_CompanyId] ON [personnel].[EmploymentStatusChanges] ([EmployeeId], [CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [personnel].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113131149_PersonnelEmploymentStatus'
)
BEGIN
    INSERT INTO [personnel].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261113131149_PersonnelEmploymentStatus', N'10.0.12');
END;

COMMIT;
GO
