# Data migration contract

The destination is a separately named, configurable SQL Server database. Never point the transfer tool or migrations at the production legacy database. The legacy `dbo` schema is read only.

| Legacy source | Destination owner | Key rule |
| --- | --- | --- |
| `dbo.Companies`, `dbo.Departments` | Organization | Preserve source IDs in an import map |
| `dbo.Employees`, `dbo.EmployeePersonalInfo`, `dbo.EmployeeJobs` | Personnel | Map employee IDs, retain administrative dates as dates |
| `dbo.Users`, `dbo.Roles` | Access | Rehash or force credential reset after algorithm review |
| `dbo.TimeOff*` | Leave | Map employee and policy IDs; compare balances |
| `dbo.Training*` | Training | Map course and employee IDs |
| `dbo.EmployeeDocuments` and private files | Documents | Copy after verifying file existence and checksums |

Each import batch must upsert by stable legacy source key, record mapping, and produce source/destination counts, missing or duplicate keys, orphan references, invalid dates, and changed-value differences. A rerun must produce no additional rows. Trial execution and reconciliation on a disposable database are required before any operational transfer. The core transfer script is implemented; other legacy tables and fields remain to be mapped. The separate Leave schema script is `database/leave-initial.sql`, regenerated with EF Core 10. It was applied twice to the disposable trial database; Leave has 3 business tables, 1 migration history table, and 2 policy foreign keys.

Initial idempotent schema scripts were generated for review in `database/access-initial.sql`, `database/organization-initial.sql`, and `database/personnel-initial.sql`. They were regenerated with EF Core 10 and rerun on the disposable trial database; EF Core 10 reports no pending model changes for any context. On 2026-11-08, the scripts were applied and rerun only in the new `HRMS_Next_Trial_20261108` database. Migration histories contain Access 1, Organization 2, Personnel 1. Organization foreign keys and Personnel rowversion were verified. One real SQL employee flow test and an HTTPS login/create/edit smoke test passed and removed their test rows. The legacy `sql/02_Create_Tables.sql` in the separate HRMS_BHR source project drops tables and must never be used for this transfer.

`database/import-core.sql` reads only `HRMS_BHR.dbo` on the same SQL Server and writes only to a destination whose name starts `HRMS_Next_`. Run it with `sqlcmd -S . -E -C -b -d HRMS_Next_Trial_20261108 -i database/import-core.sql` after applying the schema scripts. It records legacy-to-destination IDs in `migration` map tables, checks missing/duplicate references, reports source versus mapped counts and changed rows, and does not overwrite mapped values on rerun. The trial imported 1 company, 5 departments and 12 employees twice with zero invalid or changed rows. The core script does not transfer job history, personal information, users, roles, documents, training, or sensitive fields; Leave is transferred by separate scripts below.

The trial-only `database/import-leave-foundation.sql` requires the core maps and Leave schema. It reads legacy policies/balances, verifies company/employee/policy references and representable values, writes stable policy/balance maps, and rejects changed or conflicting destination rows. Run with `sqlcmd -S . -E -C -b -d HRMS_Next_Trial_20261108 -i database/import-leave-foundation.sql`. Two trial runs reconciled 4 policies and 36 balances without duplicate rows. Legacy `LastUpdated` local wall time is interpreted with SQL Server `Iran Standard Time` and preserved as a datetimeoffset. Leave requests are transferred separately as described below.

Training has an independent EF Core 10 schema script, `database/training-initial.sql`, applied and rerun on the disposable trial database (2 migrations, 2 business tables). The legacy source contains 6 global courses and 5 assignments. Transfer is pending because destination courses are company-scoped and assignment actor identity must be mapped deliberately; no Training rows were imported.

The Training SQL script sets the session options required by the filtered unique index on open assignments. The first trial application without those settings failed; after adding them, the script applied and reran successfully. SQL integration confirmed duplicate open assignments are rejected by the database.

The trial-only `database/import-leave-requests.sql` requires core employee maps, Leave policy maps, and the second Leave migration in `database/leave-initial.sql`. Run it with `sqlcmd -S . -E -C -b -d HRMS_Next_Trial_20261108 -i database/import-leave-requests.sql`. It preserves all 8 legacy requests, exact status/date/hour/text values, and requester/approver as mapped employee IDs. New runtime requests retain user IDs in separate nullable fields; no fictitious users or legacy password hashes were created. The importer verifies actors belong to the same company, reconciles every mapped value, rejects nontrial destinations, and is idempotent: two trial runs remained 8/8 with statuses Approved 3, Cancelled 1, Pending 3, Rejected 1. The legacy local `datetime` values are interpreted in `Iran Standard Time` and preserved as `datetimeoffset`.

The Leave actor-provenance migration refuses rollback while employee-attributed requests exist, preventing replacement of legacy actor identity with a fabricated user ID.
