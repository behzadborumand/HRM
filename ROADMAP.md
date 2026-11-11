# Rewrite roadmap

## Stage 0: discovery and isolation

- [x] Preserve the dirty legacy checkout; work in `D:\SourceCodes\GitProjects\HRM` on `rewrite/modular-monolith`.
- [x] Inventory legacy capabilities, schema, UI, authentication and tests in `docs/REWRITE_CAPABILITIES.md`.
- [x] Run legacy baseline tests (132 passed on 2026-11-07) and inspect the nonproduction legacy SQL database read only.

## Stage 1: working foundation

- [x] One .NET 10 Razor Pages host with independent Access, Organization and Personnel EF Core contexts/migrations.
- [x] Bootstrap/login and a company-scoped employee create/edit/search/profile flow with audit, concurrency and Jalali dates.
- [x] Native .NET 10 build and SQL/HTTPS integration on the disposable trial database.
- [x] Port the legacy HRMS_BHR RTL theme, shell and implemented employee/Leave/Training/Documents pages without redesigning the visual system.
- [ ] Complete module-specific Persian RTL UI patterns, accessibility, selection and large-list behaviors.
- [x] Administrator account provisioning, same-company employee linkage, password reset, deactivation, audit, concurrency and session revocation with SQL/HTTPS verification.
- [ ] Complete access grants, roles, company/department/employee scope and external Web API contracts.

## Stage 2: repeatable trial transfer

- [x] Core company, department and employee maps with count, relationship and changed-value reconciliation.
- [x] Leave policy/balance/request transfer with actor provenance.
- [x] Training course/assignment transfer with actor provenance.
- [x] Verify legacy Documents source count; it is currently zero.
- [x] Transfer legacy EmployeeJobs into scoped Personnel history in the trial database, with repeatable maps and full field reconciliation.
- [ ] Transfer remaining legacy tables and private files when present, with file checksums and repeatable maps.
- [ ] Plan any operational cutover only after full validation; no production transfer has occurred.

## Stage 3: modules and workflows

- [x] Leave administrator policy/request/decision/cancellation flow, SQL tests and HTTPS smoke.
- [x] Training administrator course/assignment/completion flow, SQL tests and HTTPS smoke.
- [x] Documents private list/upload/download/delete flow, SQL tests and HTTPS smoke.
- [x] Personnel job change closes dated history, updates current department and audit atomically; trial SQL and HTTPS flows pass.
- [x] Company-scoped job title catalog, archive/restore, selection in job change, trial transfer and SQL/HTTPS verification.
- [x] Department administration with scoped hierarchy, cycle rejection, archive/restore, headcounts, preserved employee history and SQL/HTTPS verification.
- [x] Organization links administration, safe active links on legacy Home layout, SQL and HTTPS flow; empty-source trial transfer.
- [x] Restore scoped Home recent hires, approved absence days, administrator pending queue and report cards in the legacy layout.
- [ ] Complete personal Leave and birthday/anniversary Home panels after employee-linked accounts and dates are ported.
- [ ] Finish manager permissions, employee self-service and document retention rules.
- [ ] Implement personnel history, job classification and position requirements, orders and contractors, appraisal, insurance and retirement, welfare and medical.
- [ ] Implement Tavanir exchange only after validating its ۱۴۰۳ contract and credential requirements.
- [x] Scoped employee/leave reports, dashboard counts, filters and capped safe CSV exports with SQL/HTTPS checks.
- [ ] Complete pay-specific and remaining reports, dynamic fields, full UI behavior and external API.

A module is complete only when its required workflows, authorization, transfer and tests pass. The current rewrite is partial and has not been published or deployed.
