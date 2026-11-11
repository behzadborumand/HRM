# BambooHR profile reference implementation plan

**Goal:** Bring the real employee profile into line with the user-provided Job and Pay Info screenshots, using the existing Persian legacy theme.
**Architecture:** Preserve site.css and tune the profile with a dedicated stylesheet. Personnel owns profile navigation, reporting relationships and private photo bytes. Razor Pages invokes those use cases directly.
**Scope:** This slice implements the existing Job/Personal/Documents profile, real photos, employee neighbours and manager/direct-report links. Payroll and unported fields require their own real data and permissions.

- [x] Add SQL regression coverage for deterministic same-company neighbour navigation, scoped direct reports and authorized photo save/read/remove with concurrency and audit.
- [x] Implement Personnel profile projections and photo storage; generate/review an idempotent EF migration and apply/rerun only on HRMS_Next_Trial_20261108.
- [x] Connect real photos and profile controls. Reuse the reference header, left rail, white active tabs and compact Job sections. Provide keyboard tab controls and a narrow layout without page overflow.
- [x] Build, run SQL/HTTPS checks and inspect desktop/mobile screenshots. Review empty photo/history states, long Persian text, reader access and private image responses.
- [x] Update checkpoint/capability mapping, commit and push to behzadborumand/HRM rewrite/modular-monolith. Refresh the local trial host without IIS deployment.

The second reference is implemented for the existing contractual pay history through a dedicated admin/linked-owner service. General employee read/write cannot view it; actual payroll/payslip data remains absent. One legacy portrait was transferred twice with path validation, stable mapping and SHA256 comparison.

Final independent review: two Important display findings fixed. Fractional pay and departure-capped tenure examples failed first, then the full suite passed 57/57. Exact screenshot fidelity was inspected separately using real 1280/919/390 px renders; local health/login/static checks passed. No deferred review finding for this slice.
