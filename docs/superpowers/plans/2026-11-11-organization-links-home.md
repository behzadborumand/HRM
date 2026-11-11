# Organization links and Home implementation plan

Goal: Restore safe, company-scoped organization links and show active links on the existing BambooHR-style Home shell.

1. Write SQL tests for URL rules, company scope, public in-company read, active filtering, stale updates and delete. Run red.
2. Add Organization-owned link model/service, migration and trial-only repeatable import. Run SQL tests and import twice.
3. Add Settings/OrganizationLinks using the legacy table/form pattern and restore a functional Home using the legacy home-workspace classes; expose only working shortcuts.
4. Extend the HTTPS smoke flow and cleanup, verify no leaked trial rows, run all tests and generated schema scripts twice.
5. Update capability/migration/checkpoint docs and push a reviewed commit to the HRM branch.
