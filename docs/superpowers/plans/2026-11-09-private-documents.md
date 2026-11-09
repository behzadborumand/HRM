# Private employee documents implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Provide an end-to-end, company-scoped private document flow on the .NET 10 rewrite.

**Architecture:** Documents owns its SQL schema, metadata, bytes, and audit fields. Personnel is consulted only through IEmployeeDirectory; Access supplies actor scope. Razor Pages calls the Documents use case directly. SQL payload storage avoids an inconsistent file/database pair and ensures files never enter wwwroot.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core 10, SQL Server, xUnit, .NET 10.

**Spec:** Original request in the attachment and legacy DocumentsController, DocumentService, DocumentRules, dbo.EmployeeDocuments.

## Global constraints

- Work only in the HRM rewrite checkout and disposable HRMS_Next_Trial_20261108 database.
- Do not modify the HRMS_BHR source or any production database.
- Read and write access must be checked in the service and scoped to the active company.
- Keep uploads to 10 MiB, allowed legacy extension list, and serve downloads as attachments.
- Administrative dates are DateOnly; upload/delete times are UTC datetimeoffset.

## Review focus

- A user from another company cannot list, download, or delete a document by guessed ID.
- Read-only staff cannot upload or delete.
- Oversized, empty, and forbidden-extension uploads fail before persistence.
- Downloads do not trust client MIME or stored path.
- An unknown employee cannot receive a document.

### Task 1: Document rules and service

**Files:** Create Modules/Documents/DocumentRules.cs, DocumentDbContext.cs, DocumentService.cs; modify Program.cs; test SqlDocumentFlowTests.cs.

- [x] Add a failing test covering validation, scoped read/write, upload/download/delete and cross-company denial.
- [x] Run the test and observe failure due to missing module.
- [x] Implement minimal schema and use cases with SQL payloads and SHA256; run test.
- [x] Generate an independent EF Core migration and idempotent script; apply and rerun on trial SQL.

### Task 2: Razor Pages flow

**Files:** Create Pages/Documents/Index.cshtml and Index.cshtml.cs; modify employee detail page for link.

- [x] Expose list, multipart upload, authenticated attachment download, and delete handlers.
- [x] Use Persian labels, Jalali expiration input, antiforgery, and clear error states.
- [x] Add an HTTPS smoke path and verify cleanup.

### Task 3: Trial reconciliation and delivery

**Files:** Update CHECKPOINT.md, ROADMAP.md, docs/REWRITE_CAPABILITIES.md, docs/REWRITE_MIGRATION.md, docs/REWRITE_LOCAL.md.

- [x] Confirm legacy document count and file presence in read-only source.
- [x] Run native suite, model parity, HTTPS smoke and trial count/cleanup checks.
- [x] Commit and push only to behzadborumand/HRM rewrite/modular-monolith.

Trial result: legacy document rows 0; new SQL module and HTTPS flow exercised on disposable rows with cleanup. No nonempty legacy file import was attempted.
