# Leave request identity and trial import — 2026-11-09

1. Preserve actor provenance: runtime requests record user IDs; legacy requests record mapped employee IDs. Make RequestedByUserId nullable and add RequestedByEmployeeId / DecidedByEmployeeId, without inventing Access accounts.
2. Generate an independent EF Core 10 Leave migration and idempotent script, apply and rerun only on HRMS_Next_Trial_20261108.
3. Add a trial-only, guarded request importer requiring core and Leave policy maps. Preserve status, hours, dates, text, and legacy actor employee IDs. Reconcile source/map/destination counts and values; rerun without new rows.
4. Run native SQL tests and HTTPS smoke with imported requests present; update migration docs, checkpoint, and push the rewrite branch.

Implemented: all four steps completed for the disposable trial. Source request count 8; mapped/destination 8 after two runs. Native tests 26/26, HTTPS smoke pass, guard rejected master, source users untouched.
