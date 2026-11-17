# Security (Web)

## Scheme selection

`DynamicAuthenticationSchemeProvider` / `TenantAuthSchemeSelector` priority:

1. Internal service headers
2. Test authentication (environment-gated)
3. Entra SSO if tenant `EntraSso:Enabled`
4. Else DfE Sign-In OIDC

Cookies are the authenticate scheme. Callbacks: `/signin-oidc`, `/signin-entra`, and test `/TestLogin`.

## Test Authentication sign-in methods

**Verified** in `Pages/TestLogin.cshtml.cs`, `Pages/TestAuthenticator.cshtml.cs`.

- `/TestLogin` asks for an email and a method: **Email me a one-time password** (default, `Input.Method=email`), **Use my authenticator app** (`authenticator`) or **I have an authenticator app setup code** (`setup-code`).
- Setup code path: `OnPostRedeemSetupCodeAsync` calls `ITokensClient.RedeemTestAuthAuthenticatorInviteAsync` (the API consumes the code and returns the key and QR URI), then `OnPostConfirmSetupCodeAsync` calls `ConfirmTestAuthAuthenticatorInviteSetupAsync` and, when valid, signs the user in through `TestAuthenticationService.AuthenticateAsync`. The QR helper is `Security/AuthenticatorQrCode` (shared with `/TestAuthenticator`). The API decides who may create codes; Web only shows the page where Test Authentication is active.
- Admins create codes at `/admin/user-manager/authenticator-code` (`CanManageUsersPolicy`, 404 unless Test Authentication is active) through `ITestAuthAuthenticatorCodeAdmin` → `IUsersClient.CreateTestAuthAuthenticatorInviteAsync`. The code is shown once and not stored by Web. Links appear on User Manager and Edit user only where Test Authentication is active. The email password stays available from every step (resend, and "Email me a one-time password instead" on the authenticator step), including after enrolment.
- Choosing the authenticator makes no API call until a code is entered, and a wrong code and "no authenticator enrolled" show the same message, so the page does not reveal who has enrolled. The code goes to `ITokensClient.VerifyTestAuthAuthenticatorCodeAsync` (service token); a valid code signs in through the same `TestAuthenticationService.AuthenticateAsync` as the email password.
- `/TestAuthenticator` (`[Authorize]`, 404 unless Test Authentication is active) lets the signed-in user set up, replace or remove their authenticator through `IUsersClient` `/v1/me/test-auth-authenticator*` (user token, so the API takes the email from the JWT). It is linked only from the Admin hub's Users & Roles card ("Authenticator app" button, shown only when Test Authentication is active), not from the main navigation. Non-admins enrol through a setup code, or can still open `/TestAuthenticator` directly once signed in.
- The API holds the pending secret. The page posts the key and `otpauth://` URI back only to show the QR code again after a wrong code; it rejects a posted URI that is not `otpauth://totp/` or is longer than `AuthenticatorQrCode.MaxAuthenticatorUriLength` (1,024), treating it as an expired setup, so a forged value cannot make QR generation throw. The QR code is a PNG data URI (CSP allows `img-src data:`).

Per-request overlays: `TenantAwareOpenIdConnectConfigurator`, `TenantAwareEntraSsoConfigurator`. Placeholder OIDC authority exists until tenant overlay runs — required so `ConfigurationManager` is not null at startup.

## Token exchange and refresh

`AddTokenRefreshWithOidc`. `ActivityBasedTokenRefreshMiddleware`, `TokenExpiryMiddleware`. Tickets in distributed cache (`DistributedCacheTicketStore`).

**Entra tenants still need `TokenRefresh:TokenEndpoint`.** `TenantTokenRefreshOptionsAccessor` validates every request. Historical production crash: empty TokenEndpoint after dropping DfE Sign-In.

## Tenant binding

`TenantIdResolver` order (README): `X-Tenant-ID` → query `tenantId` → hostname API resolve.

`TenantSessionBindingMiddleware` binds the signed-in session to its tenant. Re-read it and `TenantIdResolver` tests before changing resolver order.

## Roles in UI

`Security/AdminAccessHelper.cs`: SuperAdmin, Admin, User, plus Manage claims (`Template:Any:Manage`, …). Policies wrap those for admin folders.

## Secrets UX

`TenantSettingSecretPlaintextGate` + CSS masking. Masking is **not** authorization. Production SuperAdmin should see `__REDACTED__` and use reveal.

## Telemetry pseudonymisation

**Verified** in `Telemetry/ReportingUserIdGenerator.cs`. Application Insights user ids are HMAC-SHA256 of tenant id + `NameIdentifier` with host secret `Telemetry:ReportingUserIdKey` (Key Vault → Container App secret or API host-config, ≥32 chars, different per environment). They are pseudonymous personal data. The request log-scope `UserId` (`RequestTelemetryEnrichmentMiddleware`) uses the same id; don't put the raw `NameIdentifier` back in log scopes or `{UserId}` message properties. Missing key → no user id at all; never fall back to a raw id. Rotating the key changes every reporting id. Details: [`../docs/application-insights-reporting.md`](../docs/application-insights-reporting.md).

## Data Protection

`SharedDataProtectionExtensions` — Azure Blob + Key Vault in deployed envs. Blob name must stay **`web-keys.xml`**, distinct from the API ring. Mismatch → `CookieProtection.Unprotect` failures and forced re-login.
