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

### First request after sign-in (**Verified**, 2026-11)

Trace a sign-in bug through every step below before changing any one of them. Several steps swallow or rewrite errors, so a handler that looks right may never run (see `.ai/architecture.md`, Error boundaries).

| # | Step (file) | On failure |
|---|-------------|------------|
| 1 | OIDC callback, `Program.cs` OIDC events. `OnTokenValidated` stamps `tenant_id` | `OnAuthenticationFailed` → `/Error/ServerError`. `OnRemoteFailure`: recoverable sign-out-callback errors → local sign-out and `/`, others fall through to the default handling |
| 2 | `TenantSessionBindingMiddleware` | Cookie `tenant_id` ≠ hostname tenant → cookie sign-out, `/Error/Forbidden` |
| 3 | Api.Client `TokenManagementMiddleware` (`UseTokenManagementMiddleware`; skips `/Logout`, `/TestLogin`, `/TestLogout`) | Forced-logout flag set → sign out cookie and IdP, redirect `/` (JSON 401 for API requests). Its own exceptions are logged and the request continues |
| 4 | `ActivityBasedTokenRefreshMiddleware` | Idle or absolute timeout → `/session/timeout-sign-out?reason=…`. Its own exceptions are logged and the request continues |
| 5 | `PermissionsCacheMiddleware` → `UserPermissionsCache.RefreshAsync` → `IUsersClient.GetMyPermissionsAsync` | `RefreshAsync` **swallows every exception** (returns `null`) except the no-membership denial. So the middleware's 401/403 catches only see errors thrown elsewhere. No membership → Forbidden with reason, cookie sign-out |
| 6 | Api.Client `TokenExchangeHandler` (`DelegatingHandler` on every API call) → `POST /v1/tokens/exchange` | No EA user (404) or no membership (403) → 403 `Exchange-403-NoTenantMembership`, no forced logout. Any other failure → forced-logout flag and synthetic 401 `Exchange-401` "Authentication tokens are invalid or expired" |
| 7 | Api.Client `UserAutoRegistrationHandler`, only on `ITokensClient` (when `AutoRegisterUsers`) | Exchange 403/404/500 with "user not found" or "not a member of tenant", and an IdP issuer containing "education" or "login.microsoftonline.com" → registers the user and retries the exchange once. A failed registration returns the original response |
| 8 | API `ExchangeTokenQueryHandler` | No user → 404. No membership (not platform SuperAdmin) → 403 "User is not a member of tenant '…'". Wrong audience → 403 |
| 9 | Page code → `ExternalApiPageExceptionFilter` | Maps API errors to pages; 403 messages with "token", "expired" or "unauthorized" → `/Logout?reason=token_expired` (see `.ai/code-map.md`, Error pages) |

`UseStatusCodePages` (before authentication) turns a bare 401/403 response into a redirect to `/Error/Forbidden`.

## 4. Config

`appsettings.bootstrap.json` placeholders. Runtime merge into `ITenantRequestContext`.

## 5. Other repos

API `HostConfigController`, `TenantConfigController`, `TokensController`. CoreLibs Security.

## 6. Rules

JWT tenant_id vs hostname. Test Auth environment gate. OTP on API. Test Auth users can also sign in with an authenticator app they enrolled at `/TestAuthenticator`; the emailed password is always still offered (`.ai/security.md`, Test Authentication sign-in methods).

## 7. Edge cases

TokenEndpoint, forwarded headers, Data Protection, tenant-config cache minutes, Web vs Shared settings. Tenant config and hostname caches are per replica; admin saves clear them on every replica through `ITenantConfigurationInvalidator` (Redis pub/sub), but settings written straight to the API wait for cache expiry (`.ai/architecture.md`, Caches across replicas). Permission and role claims from `PermissionsClaimProvider` carry issuer `UserPermissionsCache.ClaimIssuer` and are rebuilt on every request (`.ai/known-pitfalls.md` item 25). A signed-in user with no membership in the tenant sees **You do not have permission**, with "You are not authorised to access this service…", and is signed out of the Web cookie (`.ai/security.md`, No membership in the tenant; `.ai/known-pitfalls.md` item 29).

## 8. Tests

Middleware and auth unit tests, Playwright TestService admin journeys.

## 9. Limits

Cannot register one OIDC scheme per tenant at startup (does not scale). Overlays must remain per-request.
