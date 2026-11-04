# Development workflows (Web)

## Local

.NET 10 SDK, running API (or deployed API + secrets), Redis for session. Launch profiles with `*.localhost` hostnames mapped in TenantConfig.

`PlatformBootstrap:Enabled=true`. User secrets flattened from `Platform` or legacy `Transfers` section in Development.

## Tests

`dotnet test GovUK.Dfe.FlexForms.Web.sln` — Domain, Application, Infrastructure.UnitTests, Web.UnitTests (includes architecture guards).

Playwright: NUnit project, product journeys (Transfers, Visits, TestService). Cypress optional/out of solution.

When adding PageModels, add Application use-case tests — not only PageModel tests.

## Template JSON

Authoring manuals in `docs/`. Template Studio is gated by FeatureManagement Target Web.

Large JSON posts historically hit ASP.NET form value limits (400). If that returns, raise form/Kestrel limits rather than truncating templates.

## Knowledge

Update `.ai/features/form-engine.md` when changing save/navigation/collection behaviour — those paths have caused data loss.
