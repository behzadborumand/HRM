# Personnel job history slice

The legacy profile shows a dated job table, while HRM currently shows only the employee's current department. The legacy trial database has 12 EmployeeJobs rows (12 current, none ended) and 8 title definitions. This slice restores the dated record contract without changing the established BambooHR inspired CSS.

## Approaches

A calculated row from the current Employee record would display a plausible table but lose title, manager, employment type, pay, and true start dates. A dedicated Personnel history table can preserve the original rows and support future job changes. Use the dedicated table. Store the title name as a historical snapshot for now, because the new Organization module does not yet own job classifications; later classification work must not rewrite historical labels.

## Scope

- Persist company scoped job rows with source employee, department, manager, title snapshot, employment type, pay fields, date range and current flag. Keep pay fields out of ordinary profile queries.
- Read history only after checking the employee belongs to the actor's company. Show the dated job table in the existing profile tab. Keep pay fields inaccessible until a separate pay authorization flow is implemented.
- Import all legacy EmployeeJobs into the disposable HRMS_Next_Trial_20261108 only, with stable source map, referential and date validation, changed value reconciliation, and repeatable execution. The legacy database remains read only.
- Do not add job editing or title administration in this slice; the existing employee edit form still updates only its current profile fields. Do not invent a synthetic history row for employees created in the new system.

## Verification

A SQL integration test covers scoped history retrieval, ordering and exclusion of pay data from the profile DTO. Apply migration and importer twice to the trial database, compare counts and changed rows, then run the full native suite and HTTPS profile flow.