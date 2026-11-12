# Home celebrations implementation plan

> Execute inline under executing-plans and test-driven-development; existing user authorization covers continuing the HRM rewrite and pushing its rewrite branch.

**Goal:** Restore the legacy Home celebrations panel with Persian-calendar birthdays and work anniversaries while keeping birth years private.
**Architecture:** Personnel projects only employee identity, next occurrence, kind and employment years. It reads birth dates internally with no tracking, joins by company/employee, excludes terminated/future hires, then returns at most6 events. Reporting composes this safe projection into Home. Razor reuses the exact legacy card classes and existing home.css.
**Stack:** Existing .NET10/EF10, Razor Pages, SQL Server; no package or schema change.
**Spec/design:** Legacy DashboardHighlightsRules.Celebrations and Views/Home/_Celebrations.cshtml; ROADMAP Home item; existing private PersonalInfos boundary. Persian month/day recurrence, clamp Esfand30 in nonleap years, today inclusive and today+30 exclusive, anniversary years>=1, sort date/kind/name/id, limit6. Admin/company directory read or write may see the card. Owner-only/team-only accounts may not. No age, birth year, contact/address, identity values or raw PersonalInfos escape the Personnel projection.

## Constraints
D:/SourceCodes/GitProjects/HRM, rewrite/modular-monolith, origin behzadborumand/HRM. Legacy HRMS_BHR checkout/SQL read only. Only HRMS_Next_Trial_20261108 may be mutated. Existing authorized checkout/branch; no new worktree. Preserve original CSS/layout. No production/IIS/publish. Positive active company actor plus directory capability required.

## Task1: scoped projection and existing Home panel
Files: create Modules/Personnel/EmployeeCelebrationService.cs, tests/HRMS.Next.Tests/SqlCelebrationTests.cs and tools/TestCelebrationsWorkflow.ps1; modify Reporting/HomeHighlightsService.cs, Program.cs, Pages/Index.cshtml, SqlHomeHighlightsTests.cs, tools/SqlWebSmoke.ps1, tools/CaptureProfile.ps1; checkpoint/roadmap/architecture.
Interface: EmployeeCelebration(EmployeeId,FullName,Date,Kind,Years), CelebrationKind Anniversary/Birthday; EmployeeCelebrationService.LoadAsync(ActorScope,DateOnly,CancellationToken). HomeHighlights adds Celebrations and HomeHighlightsService consumes the injected Personnel service.
- [x] RED: write real SQL tests and a stub. PersianWindowCrossesNowruzWithoutExposingBirthYear expects2025-03-19/20/21/22/04-17, excludes04-18 and first-year hire. EsfandThirtyClampsAndRollsForward expects2026-03-20, anniversary5. CompanyAndGrantBoundariesExcludePrivateAndInactivePeople checks foreign/departed/future/missing DOB, owner/team/inactive/zero-company denial, reader/writer/admin success, no tracked private rows. SixEventsHaveStableDateKindNameAndIdOrder checks duplicate names and cap6. Run filtered dotnet test; Expected4 behavioral failures.
- [x] GREEN: implement the minimal Personnel projection and Gregorian output event date using PersianCalendar, guard unsupported/future/private dates. No raw birth property on public contracts. Integrate Home safe DTO and original legacy markup under Highlights. Add Tehran-midnight composition regression using a fixed TimeProvider; Expected new occurrence follows Tehran day.
- [ ] HTTP RED/GREEN: real-login workflow adds generated trial-only birthday/anniversary fixtures, checks directory-reader markup/safe links/dates and no full birth date/private values; owner-only Home omits card. Query input cannot select another company. Use outer runner cleanup. Add desktop/mobile Home captures, keyboard profile link and overflow checks with generated fixture data. Expected full HTTPS smoke passes and images show original layout.
- [ ] Verify full native/SQL suite, build0 warnings/errors, full HTTPS/browser smoke, inspect Home screenshots; fresh independent final review; fix Critical/Important with RED/GREEN and rerun suite. No schema migration required.
- [ ] Update docs/ledger, refresh the owned local trial host, verify health/static files/fixture cleanup. Commit and push only HRM rewrite branch; verify origin SHA.

## Review focus
Calendar overflow/unsupported dates must not crash Home. Birth year/age/private contacts must not escape to DTO/HTML. Missing PersonalInfos must still permit anniversaries. Real role/tenant/current-employment boundaries must hold before cap6. Same-day events must retain deterministic ordering and accessible profile links without changing CSS.

## Ledger
Pre-flight: clean target c4cbd153c81d777422b04391d788507e829702fa. Legacy working tree has preexisting edits and stays untouched. Existing local verified host PID11456 remains available until replacement. No SQL migration or legacy write planned.
RED observed:4/4 SQL tests failed at the stub with NotImplementedException. Implemented Personnel-only private date processing and safe event DTO; Reporting/Razor consume only next occurrence. Legacy home.css/site.css remain unchanged.
Projection GREEN4/4. HTTP RED: old Personal-Verified build failed the newly extended Home card assertion as expected; its generated bootstrap fixture cleaned in finally. Added Tehran23:30->00:30 regression and real HTTP/browser checks. Full suite currently running; no complete claim yet.
Full SQL78/78 and build0warnings/0errors passed; initial full HTTPS/browser passed behavior. Visual/source comparison found the legacy card belongs in the sidebar before organization links, not in main content. A real HTTPS placement regression failed on Candidate-2a35d2a0 before the fix. Extracted Shared/_HomeCelebrations.cshtml, moved to the original sidebar location and restored Persian digits; added HTTP/browser layout assertions. No CSS change. Reverification follows.
