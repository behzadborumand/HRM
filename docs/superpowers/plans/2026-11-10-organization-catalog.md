# Organization catalog implementation plan

Goal: Restore company-scoped job title administration and use the catalog for employee job changes while preserving the legacy RTL layout.

1. Add a SQL integration test for catalog create, duplicate protection, company scope, update, and archival. Run red.
2. Add Organization JobTitle model, EF migration, service, and directory contract. Run green.
3. Add the legacy-pattern Settings/JobTitles Razor Page and navigation; wire job change to the title catalog without changing the global theme. Add use-case tests.
4. Add repeatable trial import for legacy titles and validation queries. Apply only to HRMS_Next_Trial_20261108 and run twice.
5. Build, run all tests, smoke the UI, update capability/migration/checkpoint docs, commit and push to the HRM remote.

No source production database changes. Existing historical job titles remain snapshots.
