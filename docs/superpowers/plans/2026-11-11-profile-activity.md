# Employee profile Leave and Training
Continue the approved BambooHR profile restoration in the existing HRM rewrite checkout. User requested continuing implementation without routine review pauses.

## Design and constraints
Read-only profile tabs, backed by module-owned DTOs through IEmployeeLeaveProfile and IEmployeeTrainingProfile. Keep the current profile shell. Admins and linked owners may read their company's employee activity; general employee read/write grants alone do not permit it. Profile route still needs normal Personnel read permission (self-service remains separate). No new writes, fake data, payroll, migrations or production deployment.

Leave: current Gregorian balance year, explicitly labelled to match existing storage; accrued, used, pending reservations, available (accrued-used-pending, preserving negative values), including archived policies with stored balances. Historical requests paginated 20 rows ordered StartDate desc, Id desc, without private reasons/comments in DTOs.
Training: assigned courses (including archived courses), provider, assignment/due/completed dates and status via existing rules; paginated 20, AssignedDate desc, Id desc. No certificate numbers/notes in profile DTO.
Both contracts verify employee through IEmployeeDirectory, then query only their own DbContext with company+employee filtering. Missing/foreign employees return null. Page parameters clamped to a real page, each tab preserves the other's page. Admin management links go to existing module screens. Owners get read-only data.

## Tasks
- [x] 1. Contracts and SQL projections. Add AccessRules.CanReadEmployeeActivity, LeaveProfileService.ReadAsync(actor,employeeId,page,ct) and TrainingProfileService.ReadAsync with DTO snapshots. Tests: administrator/owner, denied reader/writer/inactive/unlinked, foreign/nonexistent employee, year/pending math, archived courses/policies, deterministic pagination and status boundaries.
- [x] 2. Wire DI and DetailsModel, add Razor partials for Leave/Training and tab navigation. Verify full suite and HTTPS smoke for admin/owner presence and reader absence, real data IDs and values.
- [x] 3. Extend headless browser checks/screenshots for activity tabs, mobile width, hash/page retention; inspect screenshots. Independent final code review. Fix important findings with regression test first.
- [x] 4. Update checkpoint/reference/roadmap, restart verified local app copy, commit and push existing rewrite branch.

## Verification
Baseline: dotnet test HRM.sln (HRMS_TEST_CONNECTION strictly trial).
Meaningful SQL integration coverage; web smoke tools/SqlWebSmoke.ps1 with temporary generated tenant cleanup.
Browser tools/CaptureProfile.ps1 actual localhost login, keyboard tabs, 390/919/1280 widths.
No schema/model changes expected.

## Review focus
No company/employee leaks; private fields excluded; pending totals use all requests, not just a page; balance year uses Tehran current date; historical inactive records retained; no page index overflow; module boundaries.

## Progress
Existing branch rewrite/modular-monolith is the user-selected rewrite checkout; continue in place. Native Windows commands replace Bash-only skill helper scripts.
Pre-flight: projection DTOs are consumed only by DetailsModel/partials; module writers remain unchanged.

Task 1: complete. Both new SQL tests failed against empty contract stubs (expected totals 25/27, actual 0), then passed against scoped implementations. EF cannot order through a positional record constructor; the Leave query now orders/paginates its anonymous SQL projection before creating DTOs, matching the working Training query.
Task 2: complete. The new web regression failed on the previous app with missing owner activity tabs. Full suite now 59/59; build 0 warnings/errors. HTTPS smoke verifies admin/owner real records and unrelated reader exclusion, and excludes certificate/decision comments.
Task 3: complete. Full Chromium smoke passed 1280/919/390 layouts, keyboard/rapid tabs, both retained page selections/hash, mobile overflow and photo dialog. Selected mobile tab clipping was reproduced by a failing real-browser visibility assertion and fixed with scrollIntoView; the same assertion then passed. Desktop and mobile screenshots manually inspected.
Task 4: local app refreshed at https://localhost:52350, temp build HRM-Activity-Verified-3847f52e, PID 4108. Health/Login/CSS/JS HTTP200. Final SQL checks: fixture companies=0, smoke users=0, verified portrait maps=1. Documentation updated. Git records the final commit/push.

Final review: independent read-only reviewer reported no remaining Critical/Important/Minor issue. Its preliminary year-boundary test observation was addressed with a Dec31 UTC / Jan1 Tehran fixture before final verification.
Final: Ruling: production performance was not benchmarked; this is a local trial milestone, not production acceptance. Cost if wrong: slow production profile queries.
Final: Ruling: ordinary pagination can shift under concurrent writes; a cross-module consistent snapshot is not promised. Cost if wrong: transient totals/rows changing during edits.
Final: Ruling: visual behavior verified in installed Chromium at desktop/mobile widths; other browsers remain unverified. Cost if wrong: browser-specific layout differences.
Final: Ruling: profile requires Personnel read even for linked owner; full self-service remains the next slice. Cost if wrong: owner without that grant cannot enter this route yet.
Final: Ruling: unrelated modules were not independently code-reviewed in this slice; their existing suite and full HTTPS smoke passed. Cost if wrong: pre-existing defects outside this change.
No deferred minors.