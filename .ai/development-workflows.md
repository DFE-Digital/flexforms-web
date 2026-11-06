# Development workflows (Web)

## Local

.NET 10 SDK, running API (or deployed API + secrets), Redis for session. Launch profiles with `*.localhost` hostnames mapped in TenantConfig.

`PlatformBootstrap:Enabled=true`. User secrets flattened from `Platform` or legacy `Transfers` section in Development.

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
- `PW_ARTIFACTS` (`always` | `on-retry` | `off`) controls video and trace recording. CI uses `on-retry`.

When adding PageModels, add Application use-case tests — not only PageModel tests.

## Template JSON

Authoring manuals in `docs/`. Template Studio is gated by FeatureManagement Target Web.

Large JSON posts historically hit ASP.NET form value limits (400). If that returns, raise form/Kestrel limits rather than truncating templates.

## Knowledge

Update `.ai/features/form-engine.md` when changing save/navigation/collection behaviour — those paths have caused data loss.
