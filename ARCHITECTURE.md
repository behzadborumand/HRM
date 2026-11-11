# HRMS_B rewrite architecture

The new application lives under `src/HRMS.Next`, with tests in `tests/HRMS.Next.Tests`. The legacy project remains separately at `D:\SourceCodes\GitProjects\HRMS_BHR` for behavioral and migration reference; its files are not included in this rewrite working tree. The target is one ASP.NET Core 10 Razor Pages host on IIS, one SQL Server database, and a modular monolith. Each module owns its EF model, schema, DbContext, and migrations. The host calls application services directly. No module writes another module's tables.

## Boundaries

| Module | Owns | Public contract |
| --- | --- | --- |
| Access | Users, roles, credentials, grants | Current actor and permission checks |
| Organization | Companies, departments, job titles, organization links | Company, department and active title lookup; scoped catalog and link commands |
| Personnel | Employees, employment history, profile, custom fields | Employee lookup and scoped operations |
| Documents | Private files and metadata | Authorized document retrieval |
| Training | Courses and assignments | Employee ID based commands and queries |
| Leave | Policies, balances, requests | Employee ID based commands and queries |
| Reporting | Read projections | Company-scoped employee, leave and dashboard reads with capped CSV exports; no pay fields |

Job classification, position requirements, personnel orders, contractor personnel, appraisal, insurance and retirement, welfare, medical, and Tavanir have no implementation evidence in the current source. Their final boundaries and contracts require business data and rules. They are roadmap items, not completed modules.

Company and department are owned by Organization. Employee identity and employment facts are owned by Personnel. Access stores a nullable employee ID, validated through the Personnel directory contract; it does not reference Personnel EF entities. Cross-module reports may join read projections in the Reporting module; writes go through owning module application services.

## Decisions

1. One host and one database reduce deployment and transaction complexity. Separate schemas and migration histories protect ownership.
2. Razor Pages provide the primary UI; application services own validation, scope, audit, and concurrency rules.
3. Administrative dates use `DateOnly` and are displayed and entered as Jalali dates; audit instants use UTC `DateTimeOffset`.
4. Custom fields will use typed metadata and values with indexes chosen from measured queries. Adding a field must not alter the employee table.
5. Tavanir 1403 format, the single send path, and Company credential storage are unverified requirements: no message is sent until a sample contract and test credentials are available. Credentials require encryption outside source control and idempotency keys.
6. Data transfer is separate from runtime. It reads legacy `dbo` tables, writes new schemas, retains legacy IDs in mapping tables, and compares counts and relationships on every run.

No deployment, production database change, or automatic migration is performed by the host.
