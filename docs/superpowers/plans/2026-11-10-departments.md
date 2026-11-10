# Department administration implementation plan

Goal: Restore company-scoped department administration with safe hierarchy changes and archival, using the existing legacy card/table pattern.

1. Write a SQL integration test for duplicate names, cross-company parents, cycles, archival, stale updates and active lookups. Run red.
2. Add Organization-owned DepartmentService and rowversion/active columns, generate and apply migration only to the disposable trial database. Run green.
3. Preserve archived department names in employee history and search, but offer only active departments for new employee and job assignments. Keep ordinary employee edits possible when their current department is archived.
4. Add the legacy-pattern Settings/Departments Razor Page with parent options and current headcount. Extend HTTPS smoke and cleanup.
5. Run SQL and HTTPS checks, update docs, commit/push the verified milestone to the HRM branch.
