# Account administration — 2026-11-11

The legacy User table links to an employee and a role. The first Access slice adds an optional same-company employee link, unique per employee, and an administrator page in the existing settings layout. It creates only nonadministrator accounts, hashes new passwords, supports activation, employee read/write grants and password resets, writes account audits without secrets, and uses SQL rowversion for concurrency. Authentication cookies carry the account rowversion; changed accounts revoke old sessions on the next request.

The Access schema is generated with EF Core 10 and applied only to HRMS_Next_Trial_20261108 through an idempotent reviewed script. SQL tests cover company scope, foreign employee rejection, password hashing/reset, concurrency and audit. HTTPS smoke covers account creation, employee link and login. Legacy roles, passwords, user transfer, department/manager scopes and personal self-service remain separate work.
