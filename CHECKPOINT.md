# Checkpoint — 2026-11-12

Target: D:\SourceCodes\GitProjects\HRM, branch rewrite/modular-monolith, origin https://github.com/behzadborumand/HRM.git. Legacy reference: D:\SourceCodes\GitProjects\HRMS_BHR. The legacy checkout, SQL source and portrait file remain read only.

## Implemented
The rewrite is a partial .NET10/EF10 modular monolith with one Razor Pages host and independent Access, Organization, Personnel, Leave, Training and Documents schemas/contexts. Reporting owns cross-module read projections. Implemented flows include login, company employee search/profile/create/edit, audited dated job changes, titles/departments/organization links, administrator Leave/Training, private documents/photos, scoped reports/CSV, account provisioning/linkage/reset/deactivation and employee self-service.

The BambooHR profile reference is retained: green hero, overlapping portrait, grouped rail, active white tabs and compact history tables. Original site.css and home.css are unchanged. Pay is a separate admin/linked-owner projection; general employee read/write never grants pay or private Leave/Training activity. Personal /MyInfo and /My/Leave resolve their employee from the active account, without company directory grants. Personal requests use Jalali dates and versioned pending cancellation.

## Company roles and direct managers
Access now owns company Roles and RoleAudits. Users have nullable same-company RoleId with composite FK. Active role read/write combines with explicit user flags; write implies read. Custom roles cannot grant administrator. Role edits/archive, audit and assigned-user rowversion refresh are atomic, revoking earlier cookies. Active manager role assignment requires employee linkage. Archived roles retain assignments and contribute no grants.

An explicit team grant plus active same-company employee link permits current active direct-report basic profile/job/photo reads and /Team search/list. /Team/Leave is a read-only Reporting projection, filters/counts before paging20 and excludes reasons/comments. Leave decisions recheck the live relationship, deny manager self-approval, require exact request version and retain actor/time. Managers gain no company directory, reports/settings/catalog, salary, private documents/activity or write access through this grant. Full permission catalog and department grants remain pending.

Leave preserves Serializable balance reservation/consumption and bounded whole-transaction SQL1205 recovery. Earlier system_health evidence established a PK range-lock deadlock; the deterministic two-connection regression verifies both approvals complete and consume balances once. Only1205 is retried, at most3 attempts with fresh tracking after rollback; exhaustion has a Persian message.

## Verification
- Native/SQL suite:66/66 passed. Build:0 warnings,0 errors.
- Initial new role/manager tests failed against stubs before implementation. Regressions cover role company/assignment/archive, old-cookie revocation, explicit grants, current/direct/active relationship, foreign/indirect/peer exclusion, >20 paging, pay/files denial, missing/stale/replayed decisions and exact balance use.
- Full real-login HTTPS smoke passed roles/account assignment, manager profile privacy, queue scope, CSRF, reassignment, approval and archive-cookie revocation, plus all earlier Home/accounts/Personnel/job/photo/Leave/Reports/Training/Documents/owner flows.
- Headless Chromium passed desktop1280/919 and mobile390, keyboard search/tabs, overflow, profile paging/hash, private manager controls, photo dialog and real personal request/cancel. Actual screenshots were inspected. Current generated-data captures: C:\Users\Behzad\AppData\Local\Temp\HRM-Roles-20261112.
- HTTPS exposed reserved Razor route name page binding to an integer; Users/Roles/Team now use pageNumber. The complete smoke passed after this fix. The browser harness now emits a full Enter keypress; exact profile fixture Leave page2 count is8 after the additional team-approved request (Training6).
- Fresh independent read-only review found no additional Critical/Important/Minor issues. It observed the same page-binding defect before the fix.
- Access and Personnel EF model parity pass. Installed EF CLI9.0.18 warns about runtime10.0.12; the project uses EF10. SDK10.0.403 is at C:\Users\Behzad\.dotnet.

## Trial database and local host
All mutations/tests are confined to HRMS_Next_Trial_20261108. ScopedRoles migration20261112040306 and guarded database/access-scoped-roles.sql were reviewed and applied twice; Access history now has3 entries. Existing users retain their explicit grants and null role. No roles/passwords are seeded or legacy credentials transferred.

Prior trial reconciliation remains:1 company,5 departments,12 employees/jobs,8 titles,4 Leave policies,36 balances,8 requests,6 Training courses,5 assignments; legacy document/link sources are empty. One legacy portrait has a stable map and matching SHA256. Final checks found zero generated smoke/scoped-role/self-service fixture companies, zero smoke/role test users and one portrait map.

The verified local trial app runs at https://localhost:52350 from C:\Users\Behzad\AppData\Local\Temp\HRM-Roles-Verified-41dc56bc, PID21640. /health/live, /Login, /css/profile.css and /js/profile.js returned200. This is an isolated temporary build copy. No IIS, publish, production SQL or operational cutover occurred.

## Remaining
Full permission catalog/department scope; further self-service editing; personal fields/history and birthday/anniversary Home panels; bulk selection/edit; job classification/position requirements; orders/contractors; appraisal; insurance/retirement; welfare/medical; dynamic fields; remaining reports and external API. Tavanir needs a validated1403 contract and credentials. Full legacy feature parity is pending; see ROADMAP.md and docs/REWRITE_CAPABILITIES.md.