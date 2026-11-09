# Rewrite roadmap

## Stage 0: discovery and isolation

- [x] Preserve existing dirty files and create `rewrite/modular-monolith`.
- [x] Inventory current source, schema, UI, auth, and tests; see `docs/REWRITE_CAPABILITIES.md`.
- [x] Run legacy baseline tests: 132 passed on 2026-11-07.
- [ ] Inspect an actual nonproduction database and validate source documentation against it.

## Stage 1: first working slice

- [x] .NET 10 host source and independent EF contexts/migrations for Access, Organization, Personnel; native .NET 10 build passed.
- [x] Explicit bootstrap and login; use case permission and company scope checks in the initial personnel flow.
- [x] Company and department bootstrap; employee search, profile, create, edit, audit and row version checks in source.
- [ ] Complete Persian Razor Pages and UI patterns; basic forms, Jalali input/display, pagination and focus styles exist.
- [ ] Complete tests and SQL Server integration; 21 native .NET 10 tests, including Personnel and Leave SQL integration, plus an HTTPS employee and Leave smoke flow pass.

## Stage 2: transfer

- [x] First repeatable core-data transfer: company, department and employee mapping in `database/import-core.sql`; remaining tables are pending.
- [x] Core import count, relationship, invalid-data and changed-field reports; extend for each later module.
- [x] Core trial migration and rerun in `HRMS_Next_Trial_20261108`; EF Core 10 scripts regenerated and rerun.

## Stage 3: remaining modules

Leave progress: independent domain rules, DbContext/migration, and administrator request/decision service are implemented and SQL-tested on the disposable trial database. Administrator Razor Page and HTTPS smoke flow now pass; full grants, manager workflow, employee picker pagination, and import remain pending. organization and access, personnel, leave and training, documents, job classification and position requirements, orders and contractor personnel, appraisal, insurance and retirement, welfare and medical, external Tavanir exchange, reporting. A stage is complete only when its real workflows and tests pass.

## Constraints

Official SDK 10.0.403 is installed at C:\Users\Behzad\.dotnet for the current user; tools/dotnet10.ps1 selects it. The local destination is `D:\SourceCodes\GitProjects\HRM` with `origin` set to `https://github.com/behzadborumand/HRM.git`. The repository has not been pushed. Native .NET 10 restore, build, tests, EF model checks, and HTTPS smoke test passed.
