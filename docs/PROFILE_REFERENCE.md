# Profile reference implementation

The user's two BambooHR screenshots and the existing HRMS_BHR Employee Details page are the profile design reference. The rewrite keeps the original site.css unchanged; profile.css supplies the profile-specific proportions, grouped left rail, overlapping circular portrait, green hero, white active tab and compact history tables.

Implemented profile data and controls:
- Personnel-owned deterministic previous/next navigation within the actor's company.
- Same-company manager links and the first six direct reports with a total count.
- Job and employment history, Persian administrative dates and keyboard-operable tabs.
- Real private PNG/JPEG photo upload/read/delete, maximum 2 MiB, server authorization, rowversion, SHA256 verification and audit.
- A separate pay-history projection for an active same-company administrator or the linked owner. General employee read/write grants do not permit pay access. The public JobHistoryRow still excludes pay.
- Real private document metadata and download links.
- Leave profile through IEmployeeLeaveProfile: current Tehran Gregorian balance year explicitly labelled, accrued/used/all pending/available hours, and 20-row request pages; archived policy balances retained. Negative available hours remain visible.
- Training profile through IEmployeeTrainingProfile: assigned courses including archived courses, provider, dates, Tehran status, and stable 20-row pages. Both activity tabs are limited to active same-company administrators and the linked owner; employee read/write grants alone are insufficient. Reasons, decision comments and certificate numbers are excluded from these profile DTOs. Only administrators see management links.
- Activity pagination preserves both page selections and restores the selected tab. The profile route still requires general Personnel read permission; granting a link alone does not implement employee self-service.

The Pay Info reference supplies the shell and summary/history layout. Existing data contains contractual pay rates and Salary/Hourly type, without currency, taxes, deductions or paystub records. The UI therefore shows the stored rate/history and identifies the missing currency; paycheck calculations, paystub downloads and tax charts remain unimplemented.

Verification uses real SQL Server and HTTPS. Optional visual smoke creates an isolated trial fixture, captures 1280 px, 919 px and 390 px layouts using a temporary Chromium profile, checks keyboard tabs, rapid tab changes, activity pagination/hash retention, mobile page overflow, selected-tab visibility and the photo dialog, then removes all fixture records. Fixture names and pay amounts in screenshots are generated test data. The capture helper is restricted to localhost. The source portrait transfer is checked separately with stable employee/company maps, preserved bytes and SHA256 reconciliation.

Run visual verification:
powershell -NoProfile -ExecutionPolicy Bypass -File tools/SqlWebSmoke.ps1 -AppExe src/HRMS.Next/bin/Debug/net10.0/HRMS.Next.exe -DotnetRoot C:\Users\Behzad\.dotnet -Database HRMS_Next_Trial_20261108 -Port 52321 -ScreenshotsDirectory "$env:TEMP\HRM-Profile-Review"
