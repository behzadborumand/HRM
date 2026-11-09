# Checkpoint أ¢â‚¬â€‌ 2026-11-09

Target: `D:\SourceCodes\GitProjects\HRM`, branch `rewrite/modular-monolith`, origin `https://github.com/behzadborumand/HRM.git`. This branch tracks GitHub. The separate legacy source `HRMS_BHR` has not been modified by the rewrite.

The new `src/HRMS.Next` app targets .NET 10 and EF Core 10 with a Razor Pages host and separate Access, Organization, Personnel, Leave, and Training modules, DbContexts, schemas, and migrations. Implemented flows include bootstrap, login, scoped employee search/profile/create/edit, audit, rowversion, Jalali dates, administrator Leave policy/request/decision/cancellation, and administrator Training course creation/editing/activation, assignment, and completion. The rewrite is partial and is not a production replacement.

Official SDK 10.0.403 is installed for this Windows user at `C:\Users\Behzad\.dotnet`; use `tools/dotnet10.ps1`. Native build passed with zero warnings/errors. The native suite passed 26/26, including Personnel, Leave, and Training SQL flows. The HTTPS smoke flow passed employee, Leave, and Training pages; cleanup was verified with zero Smoke companies; imported Training rows remain in the trial database. The Training SQL test confirms the filtered unique index rejects duplicate open assignments.

EF Core 10 reported no pending Training model changes. Five checked-in idempotent SQL scripts were generated with EF Core 10 and applied/rerun on disposable database `HRMS_Next_Trial_20261108`. Migration histories: Access 1, Organization 2, Personnel 1, Leave 2, Training 3. The Training script includes SQL session SET options required by its filtered index.

Core import reconciled 1 company, 5 departments, and 12 employees. Trial-only Leave foundation importer reconciled 4 policies and 36 balances on two runs and rejected `master` as a destination. All 8 legacy Leave requests were imported in the trial with mapped employee actor IDs. New requests continue to record user actor IDs; no source users or credentials were invented. Legacy Training imported 6 global courses as company-scoped copies and 5 assignments into the disposable trial database; the employee actor ID is preserved separately from user identity.

Remaining work includes full grants and manager workflows, personnel history, Leave manager authorization, Training employee self-service, documents, reporting, and specialist modules. No production data change, publish, or IIS deployment has occurred. Read `ROADMAP.md` and `docs/REWRITE_MIGRATION.md` before continuing.

Training catalog continuation: added company-scoped course edit and reversible activation. SQL integration verifies both; HTTPS smoke edits a course, deactivates it, confirms it leaves the new-assignment selector, and reactivates it. Existing assignments and course history remain. No schema change was required.

Leave request continuation: Added nullable user actor plus separate employee actor fields and an EF Core 10 migration. The guarded trial-only database/import-leave-requests.sql reconciled 8/8 requests on two runs with exact source status counts (Approved 3, Cancelled 1, Pending 3, Rejected 1). It rejected master with 51200. Source actor company/reference checks passed. Native tests passed 26/26 and HTTPS employee/Leave/Training smoke passed with imported requests present; zero Smoke companies remained. Leave administrator table now shows actor provenance.

Training import continuation: database/import-training.sql guards the trial destination and reconciles 6 courses and 5 assignments on two runs. It rejects assignment datetime values with nonmidnight time, maps global courses per company, and retains employee actor provenance. The legacy database was read only.
