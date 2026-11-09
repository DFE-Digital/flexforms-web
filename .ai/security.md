# Security (Web)

## Scheme selection

`DynamicAuthenticationSchemeProvider` / `TenantAuthSchemeSelector` priority:

1. Internal service headers
2. Test authentication (environment-gated)
3. Entra SSO if tenant `EntraSso:Enabled`
4. Else DfE Sign-In OIDC

Cookies are the authenticate scheme. Callbacks: `/signin-oidc`, `/signin-entra`, and test `/TestLogin`.

Per-request overlays: `TenantAwareOpenIdConnectConfigurator`, `TenantAwareEntraSsoConfigurator`. Placeholder OIDC authority exists until tenant overlay runs — required so `ConfigurationManager` is not null at startup.

## Token exchange and refresh

`AddTokenRefreshWithOidc`. `ActivityBasedTokenRefreshMiddleware`, `TokenExpiryMiddleware`. Tickets in distributed cache (`DistributedCacheTicketStore`).

**Entra tenants still need `TokenRefresh:TokenEndpoint`.** `TenantTokenRefreshOptionsAccessor` validates every request. Historical production crash: empty TokenEndpoint after dropping DfE Sign-In.

## Tenant binding

`TenantIdResolver` order (README): `X-Tenant-ID` → query `tenantId` → hostname API resolve.

`TenantSessionBindingMiddleware` binds the signed-in session to its tenant. Re-read it and `TenantIdResolver` tests before changing resolver order.

Tenant configuration cache versions (**Verified**): `src/GovUK.Dfe.FlexForms.Web/Services/Tenant/TenantConfigurationCache.cs` keeps configuration in process memory and stores only random version markers in shared Redis, under `FlexFormsCacheKeys.WebTenantConfigurationVersionPrefix` (`src/GovUK.Dfe.FlexForms.Domain/Caching/FlexFormsCacheKeys.cs`). Redis read failures must not be treated as missing markers or permission to reuse local configuration. No tenant secrets are serialized to this cache.

## Roles in UI

`Security/AdminAccessHelper.cs`: SuperAdmin, Admin, User, plus Manage claims (`Template:Any:Manage`, …). Policies wrap those for admin folders.

## Secrets UX

`TenantSettingSecretPlaintextGate` + CSS masking. Masking is **not** authorization. Production SuperAdmin should see `__REDACTED__` and use reveal.

## Data Protection

`SharedDataProtectionExtensions` — Azure Blob + Key Vault in deployed envs. Blob name must stay **`web-keys.xml`**, distinct from the API ring. Mismatch → `CookieProtection.Unprotect` failures and forced re-login.
