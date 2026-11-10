# Preserve Legacy BambooHR UI During .NET 10 Rewrite

**Goal:** Restore the existing HRMS_BHR BambooHR-inspired presentation in HRMS.Next while retaining new module services and data bindings.

**Source of truth:** `D:\SourceCodes\GitProjects\HRMS_BHR\src\HRMS.Web\Views\Shared\_Layout.cshtml`, employee views, and `wwwroot/css/site.css`; supporting RTL Bootstrap, Bootstrap Icons, Vazirmatn and their licenses.

## Constraints

- Copy existing theme assets without redesigning colors, typography, spacing, or profile geometry.
- Adapt MVC links, forms, and conditional data to current Razor Pages routes and new models.
- Do not invent unavailable data, functional tabs, dashboard, or menu destinations.
- Preserve current form names, handlers, authorization, and SQL behavior.
- Test actual rendered HTML and screenshots against source structure on desktop and narrow viewport.
- Keep the legacy checkout read-only.

## Work

- [x] Copy theme, Bootstrap RTL, Icons and licenses; port the 48 px topbar, full-width green hero, page width, footer, search and account menu.
- [x] Port the employee list structure: hero action, filter card, result card, striped table, status, pagination.
- [x] Port the profile geometry: hero avatar/name/actions, tabs in green band, 180 px sidebar, flat content. Render only fields and destinations available from new backend.
- [x] Port form, login, Leave, Training, and Documents markup to the same Bootstrap/card system.
- [x] Remove the invented design system and correct roadmap/checkpoint descriptions.
- [x] Build, run 39 native tests and HTTPS workflow smoke; inspect desktop/mobile screenshots and static asset HTTP responses.
- [x] Restart the local app and review the diff.
