# Training legacy transfer â€” 2026-11-09

1. Preserve legacy AssignedBy employee identity separately from new user actor identity; never synthesize a user. Legacy assignment date columns are datetime but all observed values are midnight; the importer must reject nonmidnight values before converting to DateOnly.
2. Generate an independent EF Core 10 Training migration, apply and rerun the idempotent script on HRMS_Next_Trial_20261108.
3. Copy each global legacy course into each mapped company using a composite source map. Map assignments through employee/company/course maps; preserve CreatedDate as Iran-local datetimeoffset and verify every field and count on rerun.
4. Run native SQL tests and HTTPS smoke with imported Training rows present, update checkpoint and migration docs, and push the rewrite branch.

Result: 6/6 courses and 5/5 assignments reconciled on two trial runs. Native tests passed 26/26, HTTPS smoke passed, master destination was rejected, and smoke cleanup left zero Smoke companies.
