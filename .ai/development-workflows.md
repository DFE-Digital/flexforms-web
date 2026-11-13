# Development workflows (Web)

## Local

.NET 10 SDK, running API (or deployed API + secrets), Redis for session. Launch profiles with `*.localhost` hostnames mapped in TenantConfig.

`PlatformBootstrap:Enabled=true`. User secrets flattened from `Platform` or legacy `Transfers` section in Development.

To build against an unreleased `GovUK.Dfe.FlexForms.Api.Client`, create `src/GovUK.Dfe.FlexForms.Application/GovUK.Dfe.FlexForms.Application.csproj.user` (gitignored by `*.user`; MSBuild imports it automatically). In it, `<PackageReference Remove="GovUK.Dfe.FlexForms.Api.Client" />` and add a `ProjectReference` to `$(LocalFlexFormsApiSrc)GovUK.Dfe.FlexForms.Api.Client\GovUK.Dfe.FlexForms.Api.Client.csproj` (`LocalFlexFormsApiSrc` comes from `Directory.Build.props`). The Application project is the one that references the package, so a swap only in the Web project leaves both copies in the graph. If the sibling client needs an unpublished CoreLibs package, every project in the graph needs a source for it (a `NuGet.Config` above both repos works); per-project restore properties are not enough. Delete the `.user` file before relying on CI parity.

`Telemetry:ReportingUserIdKey` is optional locally; without it Web prints a `[Configuration]` line and sends no pseudonymous user ids. Deployed environments set `Telemetry__ReportingUserIdKey` from Key Vault (see `docs/application-insights-reporting.md`).

## Tests

`dotnet test GovUK.Dfe.FlexForms.Web.sln` — Domain, Application, Infrastructure.UnitTests, Web.UnitTests (includes architecture guards).

Playwright: NUnit project, product journeys (Transfers, Visits, TestService). Cypress optional/out of solution.

Playwright sets `window.Cypress = true` with `Context.AddInitScriptAsync` (`PlaywrightTestBase`), so every tab in the test gets it, including the Template Studio preview tab. A page-level init script does not reach tabs the page opens. Without the flag, a tab runs the SignalR and unread-count fetches, which can overwrite session state (see `.ai/known-pitfalls.md` #21).

Playwright parallel runs (**Verified** in test code; timings **Unverified** until a pipeline run):

- Fixtures run in parallel (`[assembly: Parallelizable(ParallelScope.Fixtures)]`, 4 workers; CI passes the same `NUnit.NumberOfTestWorkers`). Tests inside a fixture still run in order.
- `[NonParallelizable]` is for fixtures that change tenant-wide state: organisation settings, status labels, live templates, users and roles. They run on their own, after or before the parallel ones.
- Application creates go through `ApplicationCreationGate`. The API's reference generator (newest today + 1, unique index) is not safe for concurrent creates; two at once fail with a duplicate reference. This is the most likely cause of the "DB race condition" that got the first attempt at parallel runs reverted in #64 (**Unverified**).
- The API rate-limits most commands per non-admin user (e.g. 5 creates and 15 saves per 30s) and returns 429. Tests don't retry on 429, so hitting a limit shows up as a failure. UI journeys that save a lot use admin (exempt) unless the role is what's being tested.
- Don't share a browser context or session cookie between tests: overlapping requests on one session overwrite each other's session data (`.ai/known-pitfalls.md` #21).
- Put shared settings back (status labels, tenant settings) through the admin pages, not the API. Only a save through Web clears the two-level cache (Redis copy plus every replica's memory copy); a change made straight through the API stays hidden behind the cached value until it expires, and other fixtures see the test's values.
- `PW_ARTIFACTS` (`always` | `on-retry` | `off`) controls video recording. CI uses `on-retry`. Locally a Playwright trace is recorded with the video; in CI it never is (**Verified** in `PlaywrightTestBase.ContextOptions`), because a trace keeps every request header (the authentication interceptor's `x-service-api-key`) and a DOM snapshot of every page.
- Workflow artifacts in this repository are public. `playwright.yml` uploads the TRX file as the report artifact and, on failure, `test-results/` (videos and screenshots); retention is 5 days.

When adding PageModels, add Application use-case tests — not only PageModel tests.

## Template JSON

Authoring manuals in `docs/`. Template Studio is gated by FeatureManagement Target Web.

Large JSON posts historically hit ASP.NET form value limits (400). If that returns, raise form/Kestrel limits rather than truncating templates.

## Release

Trunk-based: [`docs/release-process.md`](../docs/release-process.md). `deploy.yml` promotes tagged images to production (no rebuild).

Optional production gate (**Verified** in `.github/workflows/deploy.yml`): workflow_dispatch input `required_api_release` (a flexforms-api production tag such as `v2.3.0`). After the production environment approval, the Release job polls `DFE-Digital/flexforms-api` (same org, sibling of this repo) until GitHub **Latest** is that tag or newer, then deploys Web. Blank input skips the wait (hotfixes, rollbacks, UI-only). The wait is ignored outside production. It times out after ~40 minutes.
