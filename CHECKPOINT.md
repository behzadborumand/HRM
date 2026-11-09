# Checkpoint — 2026-11-09

Target: `D:\SourceCodes\GitProjects\HRM`, branch `rewrite/modular-monolith`, origin `https://github.com/behzadborumand/HRM.git`. The rewrite branch was pushed to GitHub at commit c79cab5 before this continuation. The separate legacy source `HRMS_BHR` was not modified.

The new `src/HRMS.Next` app targets .NET 10 and EF Core 10 with Razor Pages and separate Access, Organization, Personnel, and Leave modules, DbContexts, schemas, and migrations. Implemented flows include bootstrap, login, scoped employee search/profile/create/edit, audit, rowversion, Jalali dates, and administrator Leave policy/request/decision/cancellation. The rewrite remains partial and is not a production replacement.

Official SDK 10.0.403 was SHA512 verified and installed for this Windows user at `C:\Users\Behzad\.dotnet`. Native restore/build completed with zero warnings and errors. Native tests passed 21/21, including Personnel and Leave SQL integration. The native HTTPS smoke test passed login, employee create/edit, and Leave policy/request/approval. Test rows were cleaned.

EF Core 10 reported no pending model changes for the four contexts. The checked-in idempotent SQL scripts were regenerated with EF Core 10 and rerun on disposable database `HRMS_Next_Trial_20261108`; histories remain Access 1, Organization 2, Personnel 1, Leave 1. Earlier core import reconciled 1 company, 5 departments, and 12 employees with zero invalid or changed rows.

Remaining work includes full grants and manager workflows, personnel history, Leave request identity mapping/import, training, documents, reporting, and specialist modules. No production data change, publish, or IIS deployment has occurred. The rewrite branch tracks GitHub; check Git log for the latest published commit.

2026-11-09 continuation: Added guarded trial-only database/import-leave-foundation.sql. Legacy source has 4 policies and 36 balances. The script imported and reconciled all 4/36 with stable source maps; a second run created no extra rows. Source counts remained 4/36. Legacy Leave requests are deferred because RequestedBy/ApprovedBy are employee IDs while the destination records user IDs, and six legacy requests reference an employee without a matching source user. The native suite passed 21/21 after scoping its SQL test queries to the test company; HTTPS employee and Leave smoke passed with imported data present. The importer rejected master with error 51100.
