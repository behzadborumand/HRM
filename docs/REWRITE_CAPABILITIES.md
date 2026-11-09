# Current capability inventory and transfer map

Legacy source is the separate `D:\SourceCodes\GitProjects\HRMS_BHR` repository: its `src/HRMS.Web/Controllers`, `src/HRMS.Web/Views`, `src/HRMS.Web/Services`, `src/HRMS.Data/Context`, `sql/`, and `docs/05_Project_Phases.md`. No `AGENTS.md` was found. The progress document records intent and historical completion; running code remains the evidence for behavior.

| Current capability | New owner and location | Transfer status |
| --- | --- | --- |
| Cookie login, users, roles, permission claims | Access | Partial: login and scoped administrator/user grants; full roles not transferred |
| Companies, departments, job titles, organization links | Organization; job classification boundary to be refined | Partial: company/department bootstrap and lookup only |
| Employee list, profile, CRUD, job history, bulk edit, photos | Personnel | Partial: list/profile/create/edit only; core legacy data import only; no other workflows |
| Leave policies, requests, balances, manager approval | Leave | Partial: separate schema, policy/balance/request model and admin service; administrator Razor Page and HTTPS flow; no manager grants; trial transfer covers policies, balances and all 8 requests |
| Courses and assignments | Training | Partial: independent schema, administrator course create/edit/activation, assignment/completion flow, Razor Page and SQL/HTTPS tests; 6 legacy courses and 5 assignments imported in trial; staff self-service pending |
| Private employee documents | Documents | Partial: independent schema, private paginated list/upload/download/delete, SHA256 and audit, SQL/HTTPS tests; legacy source has 0 rows; employee self-service and nonempty file transfer pending |
| Dashboard and HR reports | Reporting | Planned |
| Persian RTL shell and Jalali binder | Host presentation | Planned |
| Tavanir, contractors, orders, position requirements, appraisal, insurance, retirement, welfare, medical, dynamic fields | Corresponding future modules or Personnel metadata | No source implementation found |

Legacy uses one `HrmsDbContext` and SQL scripts in `dbo`, including a script that drops tables. Startup seeds sample organization, employees and users. The new host must not reuse either behavior. Legacy employee deletion checks some related data then physically deletes; the rewrite must preserve employment history and prefer termination/archival. Existing generic repositories and MVC service dependencies are reference material only.

The source contains SQL views `vw_EmployeeDetails` and `vw_TimeOffSummary`; no stored procedure definition was found. Table relationships are in `sql/02_Create_Tables.sql` and EF mappings. Baseline tests passed 132/132, but they do not validate a new SQL Server schema or migration. The current controller and service code requires a deeper authorization and scope audit before behavior transfer.
