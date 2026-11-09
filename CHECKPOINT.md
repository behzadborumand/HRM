# Checkpoint — 2026-11-09

Target: `D:\SourceCodes\GitProjects\HRM`, branch `rewrite/modular-monolith`, origin `https://github.com/behzadborumand/HRM.git`. Nothing has been pushed. The separate legacy source `HRMS_BHR` was not modified.

The new `src/HRMS.Next` app targets .NET 10 and EF Core 10 with Razor Pages and separate Access, Organization, Personnel, and Leave modules, DbContexts, schemas, and migrations. Implemented flows include bootstrap, login, scoped employee search/profile/create/edit, audit, rowversion, Jalali dates, and administrator Leave policy/request/decision/cancellation. The rewrite remains partial and is not a production replacement.

Official SDK 10.0.403 was SHA512 verified and installed for this Windows user at `C:\Users\Behzad\.dotnet`. Native restore/build completed with zero warnings and errors. Native tests passed 21/21, including Personnel and Leave SQL integration. The native HTTPS smoke test passed login, employee create/edit, and Leave policy/request/approval. Test rows were cleaned.

EF Core 10 reported no pending model changes for the four contexts. The checked-in idempotent SQL scripts were regenerated with EF Core 10 and rerun on disposable database `HRMS_Next_Trial_20261108`; histories remain Access 1, Organization 2, Personnel 1, Leave 1. Earlier core import reconciled 1 company, 5 departments, and 12 employees with zero invalid or changed rows.

Remaining work includes full grants and manager workflows, personnel history, Leave request identity mapping/import, training, documents, reporting, and specialist modules. No production data change, publish, IIS deployment, or Git push has occurred.
