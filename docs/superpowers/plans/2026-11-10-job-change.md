# Dated Job Change Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Record a new employee job without losing the prior row.

**Architecture:** Personnel service performs date/tenant validation, a transactional close-and-insert, employee department update and audit. Razor Pages binds a dedicated job-change handler in the existing edit screen.

**Tech Stack:** .NET 10, EF Core 10, SQL Server, Razor Pages.

**Spec:** `docs/superpowers/specs/2026-11-10-job-change.md`

## Global Constraints

- Keep the legacy presentation and leave pay data private.
- Write only the disposable HRMS_Next_Trial_20261108 during tests.
- Keep the HRMS_BHR checkout and database read only.

## Review Focus

- Stale rowversion must roll back both old-job closure and new-job insert.
- Invalid or cross-company manager must not write anything.
- Same/earlier effective date must not create overlapping history.
- A missing current job must create one initial record.
- Old pay values must carry forward without appearing in the form or public profile.

### Task 1: Service behavior

**Files:** `EmployeeJobService.cs`, `EmployeeService.cs`, `SqlJobChangeTests.cs`, `SqlEmployeeFlowTests.cs`.

- [x] Write SQL tests for first job, second job, constraints, concurrency and audit.
- [x] Run focused tests to see expected failure.
- [x] Implement transactional job change and option queries.
- [x] Run focused tests and full native suite.

### Task 2: Existing edit page

**Files:** `Edit.cshtml.cs`, `Edit.cshtml`, `JobChangeForm.cs`, `_Form.cshtml`, `Details.cshtml`, `tools/SqlWebSmoke.ps1`.

- [x] Bind the dedicated form and handler without altering personal-data submission.
- [x] Add a job-change card and profile action using the existing visual classes.
- [x] Run HTTPS create/change/profile workflow and check that pay stays private.

### Task 3: Delivery

**Files:** `CHECKPOINT.md`, `ROADMAP.md`, `docs/REWRITE_CAPABILITIES.md`.

- [x] Update capability notes with exact scope and remaining title/pay limitations.
- [x] Run full build/tests, review diff, commit and push the HRM rewrite branch.