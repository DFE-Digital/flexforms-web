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

## 6. Edge cases

Wrong FeatureManagement Target. EventMappings ≠ Prism contract. Duplicate tenant overlap of Shared/Api settings (API gate).

## 7. Tests

Application Admin tests, Web.UnitTests PageModels, Playwright admin journeys.

## 8. Limits

Studio is a visual editor over the same JSON as the designer manual — keep the model canonical; don’t invent a second schema.
