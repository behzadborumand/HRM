# Local setup for rewrite

.NET SDK 10.0.403 is installed at `C:\Users\Behzad\.dotnet`. The machine-wide `dotnet` may select SDK 9. From this repository run `powershell -NoProfile -ExecutionPolicy Bypass -File tools/dotnet10.ps1 <arguments>` to select the user-local SDK and set its runtime environment.

Use a separate SQL Server destination database and set `ConnectionStrings__HrmsNext` through environment or user secrets. Never use legacy `HRMS_BHR` as the destination. Review the database name and apply the five idempotent EF Core 10 scripts in `database/` before starting the app. The app does not migrate or seed automatically.

Set `HRMS_BOOTSTRAP_USER`, `HRMS_BOOTSTRAP_PASSWORD` (at least 14 characters), and `HRMS_BOOTSTRAP_COMPANY`, then run `tools/dotnet10.ps1 run --project src/HRMS.Next -- --bootstrap` once. Bootstrap creates a company, department, and administrator without changing an existing account. Remove the bootstrap variables afterward. Start the host with `tools/dotnet10.ps1 run --project src/HRMS.Next`.

For the disposable `HRMS_Next_Trial_20261108` database, set `HRMS_TEST_CONNECTION` and run `tools/dotnet10.ps1 test HRM.sln`. After building, run `powershell -NoProfile -ExecutionPolicy Bypass -File tools/SqlWebSmoke.ps1 -AppExe src/HRMS.Next/bin/Debug/net10.0/HRMS.Next.exe -DotnetRoot C:\Users\Behzad\.dotnet`. This checks HTTPS login, employee create/edit, and Leave policy/request/approval and Training course/assignment/completion, then cleans its test rows. The script refuses other database names. The localhost HTTPS development certificate has been trusted for this user.

The existing migrations were authored earlier and verified by EF Core 10 with no pending model changes. The checked-in SQL scripts were regenerated with EF Core 10. No publish or IIS deployment has occurred.

For the same disposable trial database, run `database/import-core.sql` and then `database/import-leave-foundation.sql` with `sqlcmd -S . -E -C -b -d HRMS_Next_Trial_20261108 -i <script>`. The Leave importer rejects other destination names and requires mapped core records. Never run either importer on the legacy database.

The Training schema script is `database/training-initial.sql`. It was generated with EF Core 10 and rerun on the disposable trial database. Apply it before using `/Training`.
