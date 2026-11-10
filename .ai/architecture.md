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

## Tenant configuration caching

**Verified.** `TenantConfigurationLoader` caches each tenant's Web configuration in the singleton `TenantConfigurationCache`, and `TenantIdResolver` caches hostname → tenant in the singleton `TenantHostnameCache`. Both are per replica and expire after `PlatformBootstrap:TenantConfigurationCacheMinutes`. `TenantConfigurationInvalidator` (`Services/Tenant/`, registered in `PlatformBootstrapExtensions`) is both the `ITenantConfigurationInvalidator` that admin pages call after a change and a hosted service that subscribes to the Redis channel `FlexFormsCacheKeys.TenantConfigurationInvalidatedChannel`, so a change on one replica clears the caches on all of them. Messages carry the sender's instance id and the sender ignores its own. The cache drops the result of a load that overlapped an invalidation, so an in-flight read cannot put old values back. If Redis is unreachable, the save still succeeds, the subscription is retried every 30 seconds, and the other replicas fall back to cache expiry. See `.ai/known-pitfalls.md` item 23.

## Use-case quartet

For each admin/form page: interface `I*`, `*Service`, `*WorkState`, `*Messages`. Outcomes: `AdminPageOutcome`, `FormEngineOutcome`. PageModels bind HTTP only.

## Historical shape

Web used to be **one tenant per container** (`APPLICATION_NAME` folders). Now hostname → API TenantConfig per request. Do not reintroduce baked tenant folders.
