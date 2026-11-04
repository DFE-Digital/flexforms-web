# Architecture

**Verified** from [`README.md`](../README.md) and `Program.cs`. Target: **.NET 10** Razor Pages + MVC JSON controllers. Not Blazor/React.

## Layers

| Layer | Project | Rule |
|-------|---------|------|
| Domain | `GovUK.Dfe.FlexForms.Domain` | Zero package refs. `FormTemplate`, `FormRouteParser`, `FormStepPolicy` |
| Application | `GovUK.Dfe.FlexForms.Application` | Use-case services, ports, outcomes. Depends on Domain + Api.Client contracts |
| Infrastructure | `GovUK.Dfe.FlexForms.Infrastructure` | API adapters, session stores, Redis, MassTransit `ScanResultConsumer`, conditional logic engine |
| Web | `GovUK.Dfe.FlexForms.Web` | Thin PageModels, middleware, auth, composition root |

Guards: `src/Tests/GovUK.Dfe.FlexForms.Web.UnitTests/Architecture/CleanArchitectureGuardTests.cs`.

## Composition

`Program.cs` requires `PlatformBootstrap:Enabled`. `AddWebLayerServices()` → `AddInfrastructureDependencyGroup()`.

## Middleware order (do not casually reorder)

Forwarded headers → original host → correlation → platform tenant configuration → exception/HSTS/security headers → HTTPS (except health) → static files → routing → cookie → **session** → auth → `TenantSessionBindingMiddleware` → token management → activity-based refresh → permissions cache → authorization → template selection → telemetry → Razor Pages/controllers.

Health: `/health`, `/healthz`, `/liveness` with `.ShortCircuit()` so probes never touch Redis session (**Verified** comment in `Program.cs` ~818).

## Use-case quartet

For each admin/form page: interface `I*`, `*Service`, `*WorkState`, `*Messages`. Outcomes: `AdminPageOutcome`, `FormEngineOutcome`. PageModels bind HTTP only.

## Historical shape

Web used to be **one tenant per container** (`APPLICATION_NAME` folders). Now hostname → API TenantConfig per request. Do not reintroduce baked tenant folders.
