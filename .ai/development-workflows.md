# Development workflows (Web)

## Local

.NET 10 SDK, running API (or deployed API + secrets), Redis for session. Launch profiles with `*.localhost` hostnames mapped in TenantConfig.

`PlatformBootstrap:Enabled=true`. User secrets flattened from `Platform` or legacy `Transfers` section in Development.

## Tests

`dotnet test GovUK.Dfe.FlexForms.Web.sln` — Domain, Application, Infrastructure.UnitTests, Web.UnitTests (includes architecture guards).

Playwright: NUnit project, product journeys (Transfers, Visits, TestService). Cypress optional/out of solution.

Playwright sets `window.Cypress = true` with `Context.AddInitScriptAsync` (`PlaywrightTestBase`), so every tab in the test gets it, including the Template Studio preview tab. A page-level init script does not reach tabs the page opens. Without the flag, a tab runs the SignalR and unread-count fetches, which can overwrite session state (see `.ai/known-pitfalls.md` #21).

When adding PageModels, add Application use-case tests — not only PageModel tests.

## Template JSON

Authoring manuals in `docs/`. Template Studio is gated by FeatureManagement Target Web.

Large JSON posts historically hit ASP.NET form value limits (400). If that returns, raise form/Kestrel limits rather than truncating templates.

## Release

Trunk-based: [`docs/release-process.md`](../docs/release-process.md). `deploy.yml` promotes tagged images to production (no rebuild).

Optional production gate (**Verified** in `.github/workflows/deploy.yml`): workflow_dispatch input `required_api_release` (a flexforms-api production tag such as `v2.3.0`). After the production environment approval, the Release job polls `DFE-Digital/flexforms-api` (same org, sibling of this repo) until GitHub **Latest** is that tag or newer, then deploys Web. Blank input skips the wait (hotfixes, rollbacks, UI-only). The wait is ignored outside production. It times out after ~40 minutes.
