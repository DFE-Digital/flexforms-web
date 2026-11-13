# Dependencies (Web)

- **flexforms-api** — TenantConfig, all writes, Api.Client NuGet (e.g. 0.0.58-prerelease.10 in Application csproj; versions drift — check the file). From 0.0.58 the client is net10.0 only and brings CoreLibs Contracts 2.x (2.0.2 for the authenticator setup-code endpoints).
- **DfE.CoreLibs** — Security, Http, Caching, Messaging, Contracts, Testing.
- **rsd-file-scanner-function / rsd-clamav-api** — indirect via bus.
- **flexforms-prism** — no package reference; reporting UI talks to API.

Local `Directory.Build.props` may point `LocalFlexFormsApiSrc` at sibling API. User targets file is gitignored.

Terraform: `terraform-azurerm-container-apps-hosting` (module version may differ from API). CI: `dotnet-ci.yml`, `deploy.yml`, `playwright.yml`, terraform workflow.

Release Flow matches API (`docs/release-process.md`): main→dev, release branch→test, tagged image→prod. GitHub `production-build` vs `production` environments. Coupled go-lives can set `required_api_release` on the Web production deploy so Release waits until that API tag is Latest.

Playwright is in the solution; Cypress under `src/Tests/GovUK.Dfe.FlexForms.CypressTests` is **not** in the `.sln`.
