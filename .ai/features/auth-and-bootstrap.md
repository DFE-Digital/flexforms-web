# Feature: Bootstrap and authentication

## 1. Purpose

Load host/tenant config from the API and authenticate users per tenant IdP.

## 2. Entry points

- `Program.cs` `BootstrapPlatformHostConfigurationAsync`
- `Middleware/TenantConfigurationMiddleware.cs`, `Services/Tenant/TenantIdResolver.cs`
- `Security/DynamicAuthenticationSchemeProvider.cs`, tenant-aware OIDC/Entra configurators
- `Controllers/SessionController.cs`, TestLogin pages

## 3. Flow

Startup app-only token → host-config. Each request: resolve tenant → tenant-config Target Web → overlay OIDC → cookie session → token exchange → API calls.

## 4. Config

`appsettings.bootstrap.json` placeholders. Runtime merge into `ITenantRequestContext`.

## 5. Other repos

API `HostConfigController`, `TenantConfigController`, `TokensController`. CoreLibs Security.

## 6. Rules

JWT tenant_id vs hostname. Test Auth environment gate. OTP on API.

## 7. Edge cases

TokenEndpoint, forwarded headers, Data Protection, tenant-config cache minutes, Web vs Shared settings. Tenant config and hostname caches are per replica; admin saves clear them on every replica through `ITenantConfigurationInvalidator` (Redis pub/sub), but settings written straight to the API wait for cache expiry (`.ai/architecture.md`, Tenant configuration caching).

## 8. Tests

Middleware and auth unit tests, Playwright TestService admin journeys.

## 9. Limits

Cannot register one OIDC scheme per tenant at startup (does not scale). Overlays must remain per-request.
