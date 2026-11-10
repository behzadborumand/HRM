# Dated job change

The legacy employee edit flow changes department, title, manager and employment type. HRM currently imports and reads dated jobs but cannot create a subsequent job. This slice adds an explicit job-change action to the existing legacy-styled edit page; personal data edit remains separate.

## Approaches

Overwriting the current row would discard history. Closing the current row and inserting a new row preserves dates and the imported history. Use the second approach in one transaction.

## Rules

Only an active actor with employee-write permission in the same company may change a job. The employee must be active. The effective date must be between hire date and today in Iran, and later than the current job's start date. The previous row ends the day before the effective date. The new date must not overlap any ended row if there is no current row. The new row becomes current and receives the chosen department, title snapshot, manager and employment type. Manager and department must belong to the actor's company; self-management is rejected. A stale employee rowversion rejects the entire change. Ordinary personal-data edit cannot change department; the edit form displays it read only. Hire date edits cannot move the date after any existing job start. The employee's current department changes atomically with the history and audit record. Pay type/rate carry forward privately from the previous job; no public DTO or form exposes them. For an employee without a prior job, create the initial row.

The form stays within the current BambooHR inspired edit page, with Jalali effective date and existing Bootstrap card and field styling. The job-title field is free text until the Organization title catalog is ported. No production database changes.

## Verification

SQL integration covers first job, second job closure, company/manager/date constraints, stale version, audit, department synchronization and preserved private pay values. HTTPS smoke covers the form and profile row. Native suite and trial database reconciliation must pass.