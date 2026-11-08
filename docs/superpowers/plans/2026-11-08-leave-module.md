# Leave Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add the leave request, balance and decision workflows to the new modular HRM app.

**Architecture:** Leave owns its SQL schema, EF DbContext, migrations, policy, balance and request records. It reads employee identity and company scope through a Personnel contract and authorizes operations through Access actor scope. Razor Pages call Leave application services; the service controls validation, transitions, balance writes and audit.

**Tech Stack:** ASP.NET Core 10 Razor Pages, EF Core 10, SQL Server, xUnit.

**Spec:** `docs/REWRITE_CAPABILITIES.md`, `ARCHITECTURE.md`, and the old HRMS_BHR TimeOffService/TimeOffRules as behavioral reference.

## Global Constraints

- Keep legacy `D:\SourceCodes\GitProjects\HRMS_BHR` unchanged.
- Leave has its own `leave` SQL schema, DbContext and migration history.
- A request and its approvals must be restricted to the actor's company.
- Use DateOnly for leave days and UTC DateTimeOffset for audit.
- Do not apply migrations to the legacy database or seed on startup.
- The current environment lacks SDK 10; any .NET 9 compatibility test is provisional.

## Review Focus

- Cross-company employee/request IDs must never leak records.
- Concurrent approval must not deduct a balance twice.
- Overlapping pending/approved leave must be rejected.
- Pending hours must reserve entitlement before another request is accepted.
- A denied or cancelled request must not consume balance.

---

### Task 1: Domain rules

**Files:** Create `src/HRMS.Next/Modules/Leave/LeaveRules.cs`; test `tests/HRMS.Next.Tests/LeaveRulesTests.cs`.

**Interfaces:** WorkingHours(DateOnly, DateOnly), ProrateAccrual(DateOnly, int, decimal), ValidateRange(DateOnly, DateOnly, DateOnly, DateOnly?).

- [x] Write tests for Friday, invalid ranges, hire/termination limits and prorated entitlement.
- [x] Run tests and observe failure for missing rules.
- [x] Implement the minimum rules.
- [x] Run tests and confirm pass; commit.

### Task 2: Persistence and migration

**Files:** Create Leave entities and `LeaveDbContext`; add migration and SQL script; register context in Program.cs.

- [x] Write EF model tests for schema, keys, constraints and concurrency.
- [x] Observe failure, implement model, generate migration.
- [x] Apply and rerun migration on disposable SQL trial; verify table/constraint counts; commit.

### Task 3: Application workflow

**Files:** Create Leave service and Personnel employee lookup contract; extend Access grants.

- [ ] Write use case tests for request, overlap, entitlement, approval, rejection, cancel, company scope and concurrent decision.
- [ ] Observe failure, implement service and transactional balance updates.
- [ ] Run tests against trial SQL, commit.

### Task 4: Razor Pages

**Files:** Create leave list/request/approval pages and navigation.

- [ ] Write HTTP tests for authorization, anti-forgery and request/decision flows.
- [ ] Observe failure, implement pages and Persian/Jalali presentation.
- [ ] Run HTTP smoke test and commit.

### Task 5: Import and reconciliation

**Files:** Extend `database/` import scripts and migration documentation.

- [ ] Inspect source policies, requests and balances; write expected counts and mapping checks.
- [ ] Implement idempotent transfer into disposable target and verify rerun.
- [ ] Record actual results in CHECKPOINT/ROADMAP and commit.