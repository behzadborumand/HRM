# Employee editor visual parity fix — 2026-11-13

Execute inline with systematic-debugging, writing-plans, TDD and one fresh final review. User explicitly rejects generic editor appearance and requires supplied BambooHR profile structure; no redesign or routine approval handoff.

## Evidence/design
User screenshot is /Employees/{id}/Edit: generic hero and boxed green section headers. _Layout activates profile.css/body profile class only for a numeric employee details path. Edit and Personal render generic forms without profile hero/avatar/sidebar/tabs. Details already has the agreed BambooHR structure. Reuse that structure for both editor routes; preserve current inputs/handlers/versions/permissions and private-data boundaries. Keep existing authorized HRM rewrite checkout, not legacy HRMS_BHR.

## Task
- [x] Add real HTTP regression requiring profile body/CSS/hero/avatar/rail/tabs on Edit; run against existing verified build and observe failure.
- [x] Add presentation-only EmployeeEditorProfileLoader/safe shell DTO, shared editor hero/sidebar partials; authorized existing page handlers load context for every render including invalid POST. Only public employment/photo summary, no pay/private reads. Personal permissions remain owner/admin.
- [x] Activate profile layout explicitly from editor views. Put existing basic/job and personal forms into the borderless profile content column, reuse existing classes/green hero/avatar and link tabs back to real profile sections. Remove generic green card bars; preserve all submit fields and anchors.
- [x] Add Edit desktop919/1280/mobile390 captures, visual and keyboard/form invalid-state assertions; Personal shells also checked. Full nativeSQL78 and real HTTPS/browser workflows. Independently review; fix meaningful findings if any. Only trial HRMS_Next_Trial_20261108 mutations; no schema/data transfer change.
- [x] Inspect actual editor images, refresh owned local trial host52350, update checkpoint, commit/push correct HRM rewrite branch and verify SHA.

## Review focus
Shell must survive invalid/stale posts without losing entered values. Owner-only Personal gets no company navigation or private peer/pay values. Writer Edit gets no private identity data. Inputs remain usable at390px; avatar and summary rail match Details at desktop. New theme detection must not change unrelated pages.
## Ledger
Base96d6b780141424eb2b90d8e780107b07a10ffaa7; target clean. Existing browser-verified local artifact Celebrations-Verified-ff2042dd remains available. Root cause confirmed by source comparison with Details and screenshot. No changes to legacy checkout.
RED confirmed: actual HTTPS Edit on prior verified build failed the new BambooHR shell assertion. Implemented presentation-only safe context and shared hero/rail; both editor views opt into existing profile layout, with scoped form styling and unchanged command fields.
NativeSQL78/78 passed. Full HTTP passed editor shell/invalid-input/writer privacy and all earlier workflows. Browser captured and actual Edit desktop/mobile images inspected: expected avatar/hero/tabs/rail and borderless editor visible. New owner-only browser assertion had an invalid CSS selector caused by quoting; replaced it with the actual manager-link class (harness only). Re-run final browser to close the gate.

Final: nativeSQL78/78; build0warnings/0errors; complete HTTPS/browser PASS after selector correction and diagnostic wait capture. Actual Edit1280/919/390 and Personal owner/admin images inspected. One fresh independent review approved with no findings; runtime/visual deferrals resolved by coordinator observed results. No remaining app defect. Source/tested DLL and profile.css hashes matched; owned local host refreshed at52350 PID10276 from Editor-Verified-d84d19f6. Imported screenshot employee EMP-0008 target ID13. Trial fixture companies/users0 and employees/private rows12/12. Final authorized Git publication is verified by terminal and remote SHA, not a self-referential commit hash in this file.
