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

Template Studio loads/saves template JSON through API version endpoints. Feature flag `TemplateStudio` in FeatureManagement Target **Web**.

## 4. Other repos

API templates, users, roles, tenant admin, reporting export proxy to Prism.

## 5. Rules

SuperAdmin-only Tenant Settings categories. Reporting acknowledgement on template save. `retiredFields` when editing live templates (manuals + API policy).

Template access for template managers (**Verified**): users with `Template:*:Manage` get the full tenant catalogue from the API, but may only work in templates they have been given. `AdminAccessHelper.HasTemplateAccess` is true for Admin/SuperAdmin, a `Template:{id}:Read`/`Write` claim, or `Template:Any:Read`/`Write`. Manage alone does not count. Templates without access are shown disabled with `AdminAccessHelper.NoTemplateAccessText`, and the server ignores them if posted, in:

- `/templates` (Choose a form) — `Pages/Templates/Index.cshtml(.cs)`
- Template Studio "Choose a template to edit" — also skipped when opening the session template or the first template
- Custom status labels (`CustomStatusLabelOverrides`) — a query/session template without access is treated as no selection
- `TemplateSelectionMiddleware` — auto-select and `?templateId=` only consider accessible templates

This is a UI rule. The API is the authority for what each call allows.

## 6. Edge cases

Wrong FeatureManagement Target. EventMappings ≠ Prism contract. Duplicate tenant overlap of Shared/Api settings (API gate).

## 7. Tests

Application Admin tests, Web.UnitTests PageModels, Playwright admin journeys.

## 8. Limits

Studio is a visual editor over the same JSON as the designer manual — keep the model canonical; don’t invent a second schema.
