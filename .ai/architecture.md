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

Application Insights: `ReportingTelemetryInitializer` must stay registered **after** `AddApplicationInsightsTelemetry` (SDK initializers run first). See `.ai/integrations.md` (Application Insights).

## Middleware order (do not casually reorder)

Forwarded headers → original host → correlation → platform tenant configuration → exception/HSTS/security headers → HTTPS (except health) → static files → routing → cookie → **session** → auth → `TenantSessionBindingMiddleware` → `ReportingSessionBindingMiddleware` → token management → activity-based refresh → permissions cache → authorization → template selection → telemetry → Razor Pages/controllers.

Health: `/health`, `/healthz`, `/liveness` with `.ShortCircuit()` so probes never touch Redis session (**Verified** comment in `Program.cs` ~818).

## Error boundaries

**Verified** (2026-11). These places swallow or rewrite errors. A catch block written above one of them may never run, so check this table, and read the method you call, before relying on a status code reaching your code. Add new swallowing points here.

| Where | What happens | Why |
|-------|--------------|-----|
| `Security/UserPermissionsCache.RefreshAsync` | Logs and returns `null` for every exception except the no-membership denial (`TenantMembershipDenial`), which it rethrows | A permissions blip must not break page loads |
| Api.Client `TokenExchangeHandler` | Every exchange failure becomes a synthetic 401 `Exchange-401` and the forced-logout flag is set, except no EA user / no membership → 403 `Exchange-403-NoTenantMembership` | Callers only see "authenticated or not" |
| Api.Client `TokenManagementMiddleware`, `Middleware/ActivityBasedTokenRefreshMiddleware` | Their own exceptions are logged and the request continues | Token housekeeping must not break the request |
| Generated client (`Client.g.cs`) | A status the endpoint doesn't declare (for example 403 on most endpoints) throws an **untyped** `ExternalApplicationsException`; the body is only in `Response`, not `Result` | NSwag only types the declared responses |
| `Filters/ExternalApiExceptionFilter` `IsAuthenticationFailureMessage`, `PermissionsCacheMiddleware.IsAuthenticationFailure` | A 403 whose message contains "token", "expired" or "unauthorized" (and some other phrases in the middleware) is treated as a logout | Word matching on API messages; avoid those words in user-facing 403 text |
| CoreLibs `DistributedCacheAdapter` | A read error is treated as a cache miss | General caches; the session adapter must **not** do this (constraint 7) |
| `UseStatusCodePages` (`Program.cs`) | A bare 401/403/404/5xx response becomes a redirect to the matching `/Error/*` page | Friendly pages for responses that have no body |

## Caches across replicas

**Verified.**

- **Templates and custom status labels use `ITwoLevelCache`** (`Infrastructure/Caching/TwoLevelCache.cs`, registered in `AddInfrastructureDependencyGroup`): 60 seconds in this replica's memory, then the shared Redis copy (`ISharedCacheStore`, with a random version per invalidation so an old fill cannot overwrite it; failed invalidations are retried in the background), then the API. Removing a key invalidates it in Redis and broadcasts it on `FlexFormsCacheKeys.LocalCacheKeyRemovedChannel` so every replica drops its memory copy. Redis is resolved on use, so startup does not depend on it. See `.ai/known-pitfalls.md` item 24.
- **Tenant configuration stays in memory per replica**, because it is read on every request, holds decrypted secrets and must keep working through a Redis outage. `TenantConfigurationLoader` caches each tenant's Web configuration in the singleton `TenantConfigurationCache`, and `TenantIdResolver` caches hostname → tenant in the singleton `TenantHostnameCache`; both expire after `PlatformBootstrap:TenantConfigurationCacheMinutes`. The cache drops the result of a load that overlapped an invalidation, so an in-flight read cannot put old values back.
- **Keeping the per-replica copies in step:** `Infrastructure/Caching/ReplicaBroadcastService.cs` publishes `{payload}|{instanceId}` on a Redis channel, subscribes as a hosted service, ignores its own messages, and retries the subscription every 30 seconds while Redis is unreachable. `TwoLevelCache` and `TenantConfigurationInvalidator` (`Services/Tenant/`, registered in `PlatformBootstrapExtensions`) derive from it. The tenant one uses channel `FlexFormsCacheKeys.TenantConfigurationInvalidatedChannel`, payload a tenant id; admin settings pages call it after a change. If Redis is unreachable the save still succeeds and other replicas fall back to cache expiry. See `.ai/known-pitfalls.md` item 23.

## Use-case quartet

For each admin/form page: interface `I*`, `*Service`, `*WorkState`, `*Messages`. Outcomes: `AdminPageOutcome`, `FormEngineOutcome`. PageModels bind HTTP only.

## Historical shape

Web used to be **one tenant per container** (`APPLICATION_NAME` folders). Now hostname → API TenantConfig per request. Do not reintroduce baked tenant folders.
