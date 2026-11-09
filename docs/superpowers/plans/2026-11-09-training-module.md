# Training vertical slice plan — 2026-11-09

1. Keep legacy Training source read only; model company-scoped courses and assignments in an independent Training DbContext and schema. Reference employees only by ID through the Personnel directory contract.
2. Implement date and course rules, admin-only create/assign/complete use cases, and company-scoped queries. Derive Pending/Overdue/Completed from dates.
3. Add a Razor Page for administrators with Jalali date input and course/employee choices. Keep domain rules out of PageModel.
4. Generate EF Core 10 migration and idempotent SQL script; apply and rerun only on HRMS_Next_Trial_20261108. Run native tests and HTTPS smoke, then document limitations and push the branch.

Implementation: steps 1-4 completed for the administrator slice. Legacy import and employee self-service are next scope. Course editing and reversible activation were added afterward and verified through SQL and HTTPS.

Final verification: 26/26 native tests, EF model parity, HTTPS employee/Leave/Training flow, and zero disposable smoke rows after cleanup. The filtered-index migration required explicit SQL SET options in the checked-in script and smoke cleanup.
