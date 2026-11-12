# Employee self-service
The existing BambooHR shell and profile are reused. This milestone provides real employee-owned reads and personal Leave workflows; configurable roles and manager/department scope remain pending.

## Provisioning
Administrator: Settings > Users, link the account to an existing employee in the same company. Leave general employee read/write unchecked for self-service-only access. The unique employee/account link and rowversion-bound cookie validation already protect linkage. Deactivation, relinking or grant changes revoke previous cookies.

## Authorization
| Workflow | Linked owner without company grants | Company reader | Administrator |
| --- | --- | --- | --- |
| Own profile, job history, photo and private document read | yes | same-company profiles | same-company profiles |
| Company employee list, neighbours, team links | no | yes | yes |
| Pay and Leave/Training profile data | own only | only if also linked owner | same company |
| Request/cancel personal Leave | own pending cancellation with exact version | only if linked owner | personal route requires employee link; admin workflows remain separate |
| Approve Leave, manage policies/courses | no | no | yes |
| Personnel/job/photo/document writes | no | no | yes; existing employee-write grant also permits Personnel/Documents writes |

AccessRules.CanRead remains the company-directory policy. CanReadEmployee adds a linked-owner alternative only for a particular positive employee ID and company. Every owning service enforces it; hiding UI is not the security boundary. Private document downloads restrict company/employee in SQL before loading bytes, then validate employee company and SHA256. Existing read/write grants retain their meaning.

## Routes and data
- /MyInfo: resolves the active account's linked employee; submitted employee IDs do not select its target.
- /My/Leave: current active policies, Jalali request form, 20-row pending pages and versioned cancellation. The employee/company always come from the account.
- Existing profile: own job/pay/activity/files, with company directory/manager hyperlinks absent for owner-only accounts.
- Home: real personal Leave balance and working profile/Leave/Training shortcuts; company highlights remain behind directory permission.
- Training stays read-only for employees. Photo/document upload/delete and employment edits retain existing write permission.
- No birthday/anniversary data or fake modules are introduced.

Create uses the existing employment dates, overlap, balance reservation and Serializable rules. Departure blocks new employee requests; administrative historical operations remain available. Cancellation requires same-company owner, Pending state and exact rowversion; SQL optimistic concurrency also covers a decision winning between SELECT and UPDATE. Actor/time are retained in request/decision metadata. Both forms bind their own inputs so request date validation cannot interfere with cancellation. Error and concurrency messages are Persian.

## Leave contention
An actual parallel SQL test exposed two Serializable approvals retaining shared PK ranges on leave.Requests and deadlocking during X conversion. The graph was read from system_health. The deterministic SQL regression reproduces the observed PK scan and synchronizes two real approvals before UPDATE. Module-local LeaveTransaction retries the entire CreateRequest/Decide operation only for SQL1205, at most three attempts. The failed transaction is disposed and rolled-back tracking discarded before re-reading. Validation, rowversion conflicts, timeouts and ambiguous commits are not automatically retried. Exhaustion returns a Persian retry message. Serializable isolation and balance safety are preserved.

## Verification
SQL tests cover no-grant ownership, company/employee boundary, file bytes, denied writes/admin operations, request metadata, overlapping/insufficient leave, inactive/unlinked/departed users, pending paging and stale/replayed cancellation. The deadlock regression verifies both approvals complete and balances are consumed once.
HTTPS checks use a no-grant employee account, crafted employee/company IDs, invalid Jalali input, missing CSRF, stale cancellation, personal file download and account revocation. Headless Chromium signs in through actual forms, captures Home/profile/Leave at desktop/mobile sizes and submits/cancels a personal request. Generated tenant fixtures are removed; all writes are confined to HRMS_Next_Trial_20261108.

Final native/SQL suite: 62/62 passed, including the real deadlock regression. Build and local HTTPS/browser checks are recorded in CHECKPOINT.md. Screenshots contain generated fictional employee/account data.
