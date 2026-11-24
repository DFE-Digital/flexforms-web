# Dependencies (Web)

- **flexforms-api** — TenantConfig, all writes, Api.Client NuGet (`0.0.60` in Application csproj, which adds the Test Auth sign-in lock endpoints; versions drift — check the file). From 0.0.58 the client is net10.0 only and brings CoreLibs Contracts 2.x. Application also pins Contracts **2.0.6** (`InviteScopedApplicationKeys` from 2.0.4; lockout fields on `VerifyTestAuthPasswordResponse` and the sign-in lock DTOs from 2.0.6).
- **DfE.CoreLibs** (net10.0 NuGet, aligned with API) — Contracts **2.0.6**, Security **2.0.2**, Caching/Http/Testing **2.0.0**, Messaging.Contracts / MassTransit **1.0.0**. Already on nuget.org latest for every CoreLibs pin in this repo. Direct `Microsoft.Extensions.*` / `TestHost` pins are **10.0.15** so they do not downgrade CoreLibs.
- **rsd-file-scanner-function / rsd-clamav-api** — indirect via bus.
- **flexforms-prism** — no package reference; reporting UI talks to API.

Local `Directory.Build.props` may point `LocalFlexFormsApiSrc` at sibling API. User targets file is gitignored.

Terraform: `terraform-azurerm-container-apps-hosting` (module version may differ from API). CI: `dotnet-ci.yml`, `deploy.yml`, `playwright.yml`, terraform workflow.

Release Flow matches API (`docs/release-process.md`): main→dev, release branch→test, tagged image→prod. GitHub `production-build` vs `production` environments. Coupled go-lives can set `required_api_release` on the Web production deploy so Release waits until that API tag is Latest.

Playwright is in the solution. There is no Cypress suite; it was removed.
