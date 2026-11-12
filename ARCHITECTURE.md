# HRMS_B rewrite architecture

The new application lives under `src/HRMS.Next`, with tests in `tests/HRMS.Next.Tests`. The legacy project remains separately at `D:\SourceCodes\GitProjects\HRMS_BHR` for behavioral and migration reference; its files are not included in this rewrite working tree. The target is one ASP.NET Core 10 Razor Pages host on IIS, one SQL Server database, and a modular monolith. Each module owns its EF model, schema, DbContext, and migrations. The host calls application services directly. No module writes another module's tables.

## Boundaries

| Module | Owns | Public contract |
| --- | --- | --- |
| Access | Users, roles, credentials, grants | Role-aware current actor, company grants, member-cookie revocation and employee-specific owner/team checks |
| Organization | Companies, departments, job titles, organization links | Company, department and active title lookup; scoped catalog and link commands |
| Personnel | Employees, employment history, profile, private portrait bytes, custom fields | Employee lookup, current direct-manager contract and scoped team/basic profile/job/photo reads; separate admin/owner pay projection |
| Documents | Private files and metadata | Authorized document retrieval |
| Training | Courses and assignments | Employee ID based commands and queries; scoped IEmployeeTrainingProfile DTO projection |
| Leave | Policies, balances, requests | Employee ID based commands and queries; scoped IEmployeeLeaveProfile DTO projection |
| Reporting | Read projections | Company reports/CSV plus paginated pending team Leave projection scoped through Personnel current jobs; no pay fields |

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

Employee-owned self-service reuses the existing module contracts and profile. MyInfo/My Leave derive identity from the current active account; no submitted employee/company ID chooses the personal target. Leave retains Serializable balance rules with bounded whole-operation SQL1205 recovery. See docs/SELF_SERVICE.md.

Access owns company Roles and RoleAudits. AppUser.RoleId is nullable with a composite same-company FK. Active role read/write grants union with explicit user flags; write implies read. IsAdmin remains a protected user property. Role edit/archive, RoleAudit and assigned-user rowversion refresh commit in one Access transaction. An inactive role retains assignments but contributes no grants. Personnel owns the live active/current direct-report relationship via IEmployeeDirectory.IsDirectManagerAsync (default deny); Leave consumes that ID-based contract and writes only its own schema. Reporting's pending team queue is a parameterized read-only SQL projection and filters/counts before 20-row paging. Manager decisions require exact version and current relationship, prohibit self-approval and preserve Serializable/retry/balance rules.
