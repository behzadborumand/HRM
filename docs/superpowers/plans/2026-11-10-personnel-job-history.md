# Personnel Job History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Preserve and display real dated legacy job rows in the new personnel profile.

**Architecture:** Personnel owns company scoped job history. A read DTO excludes pay. A trial-only SQL importer maps legacy IDs and verifies exact values.

**Tech Stack:** .NET 10, EF Core 10, Razor Pages, SQL Server.

**Spec:** `docs/superpowers/specs/2026-11-10-personnel-job-history.md`

## Global Constraints

- Preserve the existing legacy theme and profile geometry.
- Keep legacy SQL read only and apply changes only to HRMS_Next_Trial_20261108.
- Do not expose pay without a separate permission design.

## Review Focus

- Cross-company employee ID: return no history.
- Manager in another company: importer rejects the row.
- Missing title: preserve nullable title as absent.
- Rerun: no duplicate rows, all mapped fields reconcile.
- Invalid dates/current flags: fail before writing.

### Task 1: Persist and read job history

**Files:** `PersonnelDbContext.cs`, `EmployeeJobService.cs`, new Personnel migration, `SqlJobHistoryTests.cs`.

- [x] Add a failing SQL test for company scope, ordering and public DTO fields.
- [x] Run the focused test and confirm failure.
- [x] Add entity, model configuration, migration and scoped read service.
- [x] Run focused test and full native suite.

### Task 2: Transfer trial rows

**Files:** `database/import-personnel-jobs.sql`, `docs/REWRITE_MIGRATION.md`.

- [x] Add a trial-only importer with stable map, source validation and reconciliation.
- [x] Apply schema and run importer twice; compare source/mapped/changed counts.

### Task 3: Restore profile table

**Files:** `Pages/Employees/Details.cshtml.cs`, `Details.cshtml`, `CHECKPOINT.md`, `ROADMAP.md`.

- [x] Bind real history to the profile without pay fields or fabricated rows.
- [x] Verify HTTPS profile output and full suite, then commit and push HRM branch.