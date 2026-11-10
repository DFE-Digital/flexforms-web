# Feature: Admin and Template Studio

## 1. Purpose

Tenant Admin hub: templates, users, roles, settings, event mappings, new tenant, reporting export, custom status labels.

## 2. Entry points

- `Pages/Admin/*` thin PageModels
- `Application/Admin/*` use cases
- Template Studio: `Pages/Admin/TemplateStudio.cshtml.cs`, `wwwroot/js/template-studio/`
- Event mappings: `EventMappingsAdminService`, `SchemaEventDefinitionProvider`

## 3. Flow

PageModel → Admin service → Api.Client. PRG + TempData. Errors via `AdminApiErrorMapper` / `ExternalApiPageExceptionFilter`.

Template Studio loads/saves template JSON through API version endpoints. Feature flag `TemplateStudio` in FeatureManagement Target **Web**. A search question's **Name in a sentence** writes `complexField.label` and overrides that search field's FormEngine Label. **Confirmation heading** writes `confirmationTitle`; blank keeps "Is this the right {name}?".

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

Wrong FeatureManagement Target. EventMappings ≠ Prism contract.

New tenant (**Verified**): `/admin/new-tenant` (`NewTenantModel` → `NewTenantAdminService`, SuperAdmin only) no longer clones. `BuildDraft` prefills only the minimum rows from the current tenant: ConnectionStrings, Authorization (fresh secret, issuer and audience), AzureAd, Authentication, DfESignIn (always, copied; Web derives token refresh from it), the scheme's provider, and TokenRefresh when the current tenant has it. Where `TestAuthenticationEnvironmentGate.IsAllowed` (Development, Local, Test; `NewTenantModel` passes it in) the scheme defaults to **TestAuthentication**, as the old clone did: Test Authentication rows on Api and Web with `Enabled: true` and a generated signing key, issuer and audience, plus Email and EmailTemplates with an empty `TestAuthPasswordEmail` slot added when the current tenant has none. Elsewhere (Production) Test Authentication is left out and the current tenant's real scheme is kept (`ChooseScheme`; a Test-auth source falls back to DfESignIn). A required Service name field is prefilled from the current tenant's `Layout:ServiceName` (Web row before Shared, else the tenant name); on create `WithServiceName` writes it into the Layout row Web reads (the copied Layout row's other keys are kept), adding a Web Layout row if none is left. The API requires `Layout:ServiceName`. Missing rows become empty placeholders. After those, `CopiedCategories` (FeatureManagement, FileStorage, Template, NotificationService, NotificationBanner, MassTransit, FrontendSettings, FormEngine, ExternalApplicationsApiClient, EntraSso, DfESignIn, Email, CacheSettings, ApplicationTerminology, ApplicationTemplates, ApplicationReference) are copied as-is when the current tenant has them, flagged `IsOptional`, and shown under "Copied from the current tenant". Copied template mappings are usually rejected by the API's ownership check, and the page hint says so. The API also rejects any template mapping when the new tenant uses a different EA database from the current tenant. Each row is an editable JSON textarea; blank means "leave out", and `__REDACTED__` keeps the current tenant's value (the API restores it). The API's `NewTenantSettingsRequirements` is the authority and rejects anything else, so keep `BuildDraft` in step with it. API validation messages are shown as-is via `AdminApiErrorMapper`. Deploy Web and API together: the `payloadJson` shape changed on the same `POST .../clone` endpoint.

## 7. Tests

Application Admin tests, Web.UnitTests PageModels, Playwright admin journeys.

## 8. Limits

Studio is a visual editor over the same JSON as the designer manual — keep the model canonical; don’t invent a second schema.
