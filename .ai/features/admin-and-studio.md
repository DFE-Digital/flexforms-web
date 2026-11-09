# Feature: Admin and Template Studio

## 1. Purpose

Tenant Admin hub: templates, users, roles, settings, event mappings, duplicate tenant, reporting export, custom status labels.

## 2. Entry points

- `Pages/Admin/*` thin PageModels
- `Application/Admin/*` use cases
- Template Studio: `Pages/Admin/TemplateStudio.cshtml.cs`, `wwwroot/js/template-studio/`
- Event mappings: `EventMappingsAdminService`, `SchemaEventDefinitionProvider`

## 3. Flow

PageModel → Admin service → Api.Client. PRG + TempData. Errors via `AdminApiErrorMapper` / `ExternalApiPageExceptionFilter`.

Tenant configuration refresh (**Verified**): Organisation Settings, Tenant Settings mutations/refresh and Event Mappings await `ITenantConfigurationCache.InvalidateAsync` before reporting success. It changes a shared Redis version marker; each Web replica checks that marker before reusing local configuration. Sources: `src/GovUK.Dfe.FlexForms.Web/Pages/Admin/OrganisationSettings.cshtml.cs`, `TenantSettings.cshtml.cs`, `EventMappings.cshtml.cs`, and `Services/Tenant/TenantConfigurationCache.cs`. API refresh still happens first; this does not change API-side cache invalidation.

Template Studio loads/saves template JSON through API version endpoints. Feature flag `TemplateStudio` in FeatureManagement Target **Web**.

## 4. Other repos

API templates, users, roles, tenant admin, reporting export proxy to Prism.

## 5. Rules

SuperAdmin-only Tenant Settings categories. Reporting acknowledgement on template save. `retiredFields` when editing live templates (manuals + API policy).

Template access for template managers (**Verified**): users with `Template:*:Manage` get the full tenant catalogue from the API, but may only work in templates they have been given. `AdminAccessHelper.HasTemplateAccess` is true for Admin/SuperAdmin; for a template manager it needs a `Template:{id}:Read`/`Write` claim or `Template:Any:Read`/`Write` (Manage alone does not count). Everyone else is not filtered: the API already returned only their templates, and Caseworkers (`Application:Any:Read`) have no Template grants at all.

Two checks, on purpose: `HasTemplateAccess` gates template **management** (Admin table, Template Studio, custom statuses). `CanOpenTemplate` gates **entering** a template's applications (Choose a form, `TemplateSelectionMiddleware`) and also passes anyone with `CanReadAnyApplication`, so a Caseworker who is also a template manager keeps their application access. Templates without access are shown disabled with `AdminAccessHelper.NoTemplateAccessText`, and the server ignores them if posted, in:

- Admin hub "Tenant templates" table — Open/Preview and Make live/not live are disabled; the handlers reject them too
- `/templates` (Choose a form) — `Pages/Templates/Index.cshtml(.cs)`, using `CanOpenTemplate`
- Template Studio "Choose a template to edit" — also skipped when opening the session template or the first template
- Custom status labels (`CustomStatusLabelOverrides`) — a query/session template without access is treated as no selection
- `TemplateSelectionMiddleware` — auto-select and `?templateId=` only consider templates that pass `CanOpenTemplate`

This is a UI rule. The API is the authority for what each call allows.

Template Studio for non-admins (**Verified**): `GET /v1/Roles` is Admin/SuperAdmin only, so `TemplateStudioModel` skips it and sends `canChooseContributorRole: false`; the JS shows a note instead of the contributor-role picker and keeps the saved `contributorRoleId`, even if invitations are turned off and on again. The application count needs `CanReadAnyApplication`, so it is skipped and sent as unknown (`null`), which Template Studio treats as "in use".

Template Studio front end (**Verified**): GOV.UK components that `template-studio.js` draws (accordions, radios, checkboxes, error summaries, banners) are started by `enhance()`, which waits for its own GOV.UK Frontend import if that hasn't finished yet (see `.ai/known-pitfalls.md` #22).

Template Studio preview (**Verified**): the preview keeps its template and answers in session only, so they can be lost to overlapping requests (see `.ai/known-pitfalls.md` #21).

## 6. Edge cases

Wrong FeatureManagement Target. EventMappings ≠ Prism contract. Duplicate tenant overlap of Shared/Api settings (API gate).

## 7. Tests

Application Admin tests, Web.UnitTests PageModels, Playwright admin journeys.

## 8. Limits

Studio is a visual editor over the same JSON as the designer manual — keep the model canonical; don’t invent a second schema.
