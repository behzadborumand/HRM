-- Trial-only repeatable import. The legacy database is read-only.
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'HRMS_Next_Trial_20261108'
    THROW 51700, 'Only the named disposable trial database is allowed.', 1;
IF DB_ID(N'HRMS_BHR') IS NULL OR OBJECT_ID(N'organization.OrganizationLinks',N'U') IS NULL
    THROW 51701, 'Source or destination organization links table is missing.', 1;
IF OBJECT_ID(N'migration.CompanyMap',N'U') IS NULL
    THROW 51702, 'Company map is required.', 1;
IF EXISTS (SELECT 1 FROM HRMS_BHR.dbo.Companies c LEFT JOIN migration.CompanyMap m ON m.LegacyCompanyId=c.CompanyId WHERE m.CompanyId IS NULL)
    THROW 51703, 'A legacy company is not mapped.', 1;
IF EXISTS (
    SELECT 1 FROM HRMS_BHR.dbo.OrganizationLinks l
    WHERE l.Title IS NULL OR LEN(LTRIM(RTRIM(l.Title))) NOT BETWEEN 1 AND 100
       OR l.Url IS NULL OR LEN(LTRIM(RTRIM(l.Url))) NOT BETWEEN 1 AND 2000
       OR LEN(l.Description)>300 OR l.SortOrder NOT BETWEEN 0 AND 9999 OR l.Active IS NULL
       OR (LOWER(l.Url) NOT LIKE 'https://%' AND LOWER(l.Url) NOT LIKE 'http://%')
       OR l.Url LIKE '%' + CHAR(10) + '%' OR l.Url LIKE '%' + CHAR(13) + '%'
       OR l.Url LIKE '%' + CHAR(9) + '%')
    THROW 51704, 'A legacy link cannot be represented or requires URL review.', 1;
SELECT cm.LegacyCompanyId, cm.CompanyId, l.LinkId AS LegacyLinkId,
       LTRIM(RTRIM(l.Title)) AS Title, LTRIM(RTRIM(l.Url)) AS Url,
       NULLIF(LTRIM(RTRIM(l.Description)),N'') AS Description, l.SortOrder, l.Active
INTO #SourceLinks
FROM migration.CompanyMap cm CROSS JOIN HRMS_BHR.dbo.OrganizationLinks l;
IF (SELECT COUNT(*) FROM #SourceLinks)<>(SELECT COUNT(*) FROM migration.CompanyMap)*(SELECT COUNT(*) FROM HRMS_BHR.dbo.OrganizationLinks)
    THROW 51705, 'Link projection lost or duplicated rows.', 1;
BEGIN TRANSACTION;
IF OBJECT_ID(N'migration.OrganizationLinkMap',N'U') IS NULL
    CREATE TABLE migration.OrganizationLinkMap (
        LegacyCompanyId int NOT NULL, LegacyLinkId int NOT NULL, LinkId int NOT NULL UNIQUE,
        CONSTRAINT PK_OrganizationLinkMap PRIMARY KEY (LegacyCompanyId,LegacyLinkId));
IF EXISTS (SELECT 1 FROM migration.OrganizationLinkMap m LEFT JOIN organization.OrganizationLinks d ON d.Id=m.LinkId WHERE d.Id IS NULL)
    THROW 51706, 'A mapped destination link is missing.', 1;
IF EXISTS (SELECT 1 FROM migration.OrganizationLinkMap m LEFT JOIN #SourceLinks s ON s.LegacyCompanyId=m.LegacyCompanyId AND s.LegacyLinkId=m.LegacyLinkId WHERE s.LegacyLinkId IS NULL)
    THROW 51707, 'A mapped legacy link has disappeared.', 1;
IF EXISTS (
    SELECT 1 FROM #SourceLinks s
    LEFT JOIN migration.OrganizationLinkMap m ON m.LegacyCompanyId=s.LegacyCompanyId AND m.LegacyLinkId=s.LegacyLinkId
    JOIN organization.OrganizationLinks d ON d.CompanyId=s.CompanyId AND d.Title=s.Title AND d.Url=s.Url
    WHERE m.LinkId IS NULL)
    THROW 51708, 'An unmapped destination link conflicts with the legacy source.', 1;
MERGE organization.OrganizationLinks AS target
USING (
    SELECT s.* FROM #SourceLinks s
    LEFT JOIN migration.OrganizationLinkMap m ON m.LegacyCompanyId=s.LegacyCompanyId AND m.LegacyLinkId=s.LegacyLinkId
    WHERE m.LinkId IS NULL
) AS source ON 1=0
WHEN NOT MATCHED THEN INSERT (CompanyId,Title,Url,Description,SortOrder,Active)
    VALUES (source.CompanyId,source.Title,source.Url,source.Description,source.SortOrder,source.Active)
OUTPUT source.LegacyCompanyId,source.LegacyLinkId,inserted.Id
INTO migration.OrganizationLinkMap (LegacyCompanyId,LegacyLinkId,LinkId);
IF EXISTS (
    SELECT 1 FROM #SourceLinks s
    JOIN migration.OrganizationLinkMap m ON m.LegacyCompanyId=s.LegacyCompanyId AND m.LegacyLinkId=s.LegacyLinkId
    JOIN organization.OrganizationLinks d ON d.Id=m.LinkId
    WHERE EXISTS (SELECT d.CompanyId,d.Title,d.Url,d.Description,d.SortOrder,d.Active
                  EXCEPT SELECT s.CompanyId,s.Title,s.Url,s.Description,s.SortOrder,s.Active))
    THROW 51709, 'A mapped link differs from its legacy source.', 1;
IF (SELECT COUNT(*) FROM migration.OrganizationLinkMap)<>(SELECT COUNT(*) FROM #SourceLinks)
    THROW 51710, 'Link map count differs from source projection.', 1;
COMMIT TRANSACTION;
SELECT (SELECT COUNT(*) FROM #SourceLinks) AS SourceLinks,
       (SELECT COUNT(*) FROM migration.OrganizationLinkMap) AS MappedLinks,
       (SELECT COUNT(*) FROM organization.OrganizationLinks d JOIN migration.OrganizationLinkMap m ON m.LinkId=d.Id) AS PresentLinks;
