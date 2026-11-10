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

## Caches across replicas

**Verified.**

- **Templates and custom status labels use `ITwoLevelCache`** (`Infrastructure/Caching/TwoLevelCache.cs`, registered in `AddInfrastructureDependencyGroup`): 60 seconds in this replica's memory, then the shared Redis copy (`ISharedCacheStore`, with a random version per invalidation so an old fill cannot overwrite it; failed invalidations are retried in the background), then the API. Removing a key invalidates it in Redis and broadcasts it on `FlexFormsCacheKeys.LocalCacheKeyRemovedChannel` so every replica drops its memory copy. Redis is resolved on use, so startup does not depend on it. See `.ai/known-pitfalls.md` item 24.
- **Tenant configuration stays in memory per replica**, because it is read on every request, holds decrypted secrets and must keep working through a Redis outage. `TenantConfigurationLoader` caches each tenant's Web configuration in the singleton `TenantConfigurationCache`, and `TenantIdResolver` caches hostname → tenant in the singleton `TenantHostnameCache`; both expire after `PlatformBootstrap:TenantConfigurationCacheMinutes`. The cache drops the result of a load that overlapped an invalidation, so an in-flight read cannot put old values back.
- **Keeping the per-replica copies in step:** `Infrastructure/Caching/ReplicaBroadcastService.cs` publishes `{payload}|{instanceId}` on a Redis channel, subscribes as a hosted service, ignores its own messages, and retries the subscription every 30 seconds while Redis is unreachable. `TwoLevelCache` and `TenantConfigurationInvalidator` (`Services/Tenant/`, registered in `PlatformBootstrapExtensions`) derive from it. The tenant one uses channel `FlexFormsCacheKeys.TenantConfigurationInvalidatedChannel`, payload a tenant id; admin settings pages call it after a change. If Redis is unreachable the save still succeeds and other replicas fall back to cache expiry. See `.ai/known-pitfalls.md` item 23.

## Use-case quartet

For each admin/form page: interface `I*`, `*Service`, `*WorkState`, `*Messages`. Outcomes: `AdminPageOutcome`, `FormEngineOutcome`. PageModels bind HTTP only.

## Historical shape

Web used to be **one tenant per container** (`APPLICATION_NAME` folders). Now hostname → API TenantConfig per request. Do not reintroduce baked tenant folders.
