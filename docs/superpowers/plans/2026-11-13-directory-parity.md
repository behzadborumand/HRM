# Employee directory/create parity — 2026-11-13

Execute inline with writing-plans/TDD and one fresh final review; user requested continuation after BambooHR editor fix, keeping the previous visual system.

## Design
Legacy Employees/Index has current title, manager name and real portraits in its existing table, omitted by the rewrite's narrow row DTO. Restore those public fields in a bounded company-scoped page projection, never salary/private contact data or photo bytes. Preserve original filter/table layout and actions, add real name/profile links and Persian counts. Employee page uses reserved Razor parameter page; replace with pageNumber as other corrected pages. Create retains its real inputs/actions and adopts the compact white editor form style; no fictitious employee portrait before creation.

## Constraints
D:/SourceCodes/GitProjects/HRM, rewrite/modular-monolith, origin behzadborumand/HRM. Legacy HRMS_BHR source and SQL read only. Only HRMS_Next_Trial_20261108 mutations. No migrations, production or IIS. Existing user authorization covers inline continuation and pushing the same branch.

## Task
- [x] RED: add optional directory facts to EmployeeRow as empty seams and actual SQL tests. CurrentTitleManagerPortraitAreSafeAndTenantScoped must fail on missing metadata; PageMetadataDoesNotChangeOrderingFiltersOrBounds characterizes existing native paging. A real HTTP25-row fixture (24active/1departed plus foreign match) must show pageNumber2 has4 rows, not20 on old build.
- [x] GREEN: enrich only paged IDs with current same-company job, same-company manager and same-company photo SHA; no per-row queries/bytes/private model. Keep no-job/photo placeholders. Fix Employees Index pageNumber parameter and all links, preserve query/department/status. Restore current title/manager/portrait columns and Persian count/range.
- [x] Create uses explicit existing profile-form styling with white section headers and existing field/handler behavior; malformed POST preserves entered fields. Add actual HTTP and browser checks.
- [x] Full nativeSQL suite and build; complete real HTTPS/browser; inspect directory/Create desktop/mobile screenshots and exercise filters, next page, keyboard and real photo loading. Review independently; fix Critical/Important with tests.
- [x] Refresh owned verified local host52350; cleanup fixtures and document outcomes. Commit/push correct HRM rewrite branch and verify remote SHA.

## Review focus
Historical jobs must not duplicate employee rows or replace current title. Inconsistent foreign metadata/manager links must not cross tenants. A portrait page projection must not fetch bytes or private fields. Page2 links must preserve all filters and expose only20 rows per page. Create invalid states retain the new styling/values, and reader/owner/team grants remain unchanged.

## Ledger
Base62864a4094f781a961dde47f53692228dbda2356, clean target. Original legacy list structure inspected; title/manager/photo fields missing. Current native Search already pages before UI, but Index binds reserved page. Existing local trial host Editor-Verified-d84d19f6 PID10276 remains until verified replacement.
SQL RED: CurrentTitleManagerPortraitAreSafeAndTenantScoped failed (CURRENT_TITLE expected, null actual); native page bounds/order characterization passed before enrichment. Implemented bounded current-job/manager/photo metadata projection and legacy table fields, pageNumber route, Persian counts/range and white Create form style. HTTP old-build paging reproduction is running independently.
Actual HTTP RED on old build: pageNumber2 returned the first20 rows instead of the final4; generated trial/foreign fixtures cleaned. Fixed an initial Razor compile marker on the empty-state row (employee exists only inside foreach). Valid fixture PNG bytes match the existing known smoke PNG.
Full nativeSQL80/80, build0warnings/errors and complete HTTPS/browser passed. Actual directory/Create images inspected. Visual review exposed Create header collapse: page-profile removes generic hero padding, while Create lacked profile-hero. New actual HTTP gate failed against Candidate134136dd before correction. Create now uses the real profile-hero structure without portrait padding; browser also checks >=90px hero height. Reverification follows.

Final: nativeSQL80/80 and build0warnings/0errors; final candidate3696bc22 passed complete HTTPS/browser including >=90px Create hero, filters,20+4 paging, actual portrait and invalid form retention. Updated Create desktop/mobile captures inspected. Independent review found no Critical/Important/Minor findings; runtime/visual evidence completed by coordinator. Owned host10276 was identity/port checked, then replaced by PID23400 at52350 using the exact tested artifact. DLL/CSS source hashes matched. Four live endpoints returned200. Final trial fixture companies/users0; employees12/private rows12. CHECKPOINT/ROADMAP/capability inventory updated. Commit/push follows on the verified HRM rewrite branch.