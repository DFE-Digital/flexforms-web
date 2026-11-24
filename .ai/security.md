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
- Admins create codes at `/admin/user-manager/authenticator-code` (`CanManageUsersPolicy`, 404 unless Test Authentication is active) through `ITestAuthAuthenticatorCodeAdmin` → `IUsersClient.CreateTestAuthAuthenticatorInviteAsync`. The code is shown once and not stored by Web. The create link appears on Edit user only where Test Authentication is active. The email password stays available from every step (resend, and "Email me a one-time password instead" on the authenticator step), including after enrolment.
- Lockouts (**Verified**, `TestLoginModel.PermanentLockoutMessage` / `TemporaryLockoutMessage`): when `VerifyTestAuthPasswordResponse.IsPermanentLockout` is true (an administrator locked the email) or the API returns **423 Locked** (send password, redeem setup code), the page shows the contact-your-tenant-administrator message and nothing else happens. When the authenticator check returns `IsLockedOut` without `IsPermanentLockout`, it shows how long to wait, rounded up to whole minutes from `RetryAfterSeconds`, and points to the emailed password. Keep the permanent message the same for every locked email.
- Choosing the authenticator makes no API call until a code is entered, and a wrong code and "no authenticator enrolled" show the same message, so the page does not reveal who has enrolled. The code goes to `ITokensClient.VerifyTestAuthAuthenticatorCodeAsync` (service token); a valid code signs in through the same `TestAuthenticationService.AuthenticateAsync` as the email password.
- `/TestAuthenticator` (`[Authorize]`, 404 unless Test Authentication is active) lets the signed-in user set up, replace or remove their authenticator through `IUsersClient` `/v1/me/test-auth-authenticator*` (user token, so the API takes the email from the JWT). It is linked only from the Admin hub's Users & Roles card ("Authenticator app" button, shown only when Test Authentication is active), not from the main navigation. Non-admins enrol through a setup code, or can still open `/TestAuthenticator` directly once signed in.
- The API holds the pending secret. The page posts the key and `otpauth://` URI back only to show the QR code again after a wrong code; it rejects a posted URI that is not `otpauth://totp/` or is longer than `AuthenticatorQrCode.MaxAuthenticatorUriLength` (1,024), treating it as an expired setup, so a forged value cannot make QR generation throw. The QR code is a PNG data URI (CSP allows `img-src data:`).

Per-request overlays: `TenantAwareOpenIdConnectConfigurator`, `TenantAwareEntraSsoConfigurator`. Placeholder OIDC authority exists until tenant overlay runs — required so `ConfigurationManager` is not null at startup.

## Token exchange and refresh

`AddTokenRefreshWithOidc`. `ActivityBasedTokenRefreshMiddleware`, Api.Client `TokenManagementMiddleware` (Web's `TokenExpiryMiddleware` exists but is not registered). Tickets in distributed cache (`DistributedCacheTicketStore`).

**Entra tenants still need `TokenRefresh:TokenEndpoint`.** `TenantTokenRefreshOptionsAccessor` validates every request. Historical production crash: empty TokenEndpoint after dropping DfE Sign-In.

## Tenant binding

`TenantIdResolver` order (README): `X-Tenant-ID` → query `tenantId` → hostname API resolve.

`TenantSessionBindingMiddleware` binds the signed-in session to its tenant: a `tenant_id` claim for another tenant signs out the cookie and redirects to `/Error/Forbidden`; a missing claim (cookies issued before it existed), a non-GUID claim or no resolved tenant is let through. Re-read it, `TenantSessionBindingMiddlewareTests` and the `TenantIdResolver` tests before changing resolver order.

**No access to the tenant** (**Verified**, `Security/TenantMembershipDenial.cs`, `Middleware/PermissionsCacheMiddleware.cs`, `Filters/ExternalApiExceptionFilter.cs`). Someone who signs in successfully but has no EA user (API exchange **404** "User not found for email") or no membership (API **403** "not a member of tenant") is denied at token exchange. Api.Client `TokenExchangeHandler` turns both into a 403 with `ErrorId` `Exchange-403-NoTenantMembership`; older clients returned a generic 401 and Web sent them to `/Logout?reason=auth_failed`. This is authorisation, not an expired session. `UserPermissionsCache.RefreshAsync` rethrows it (it swallows every other failure). `PermissionsCacheMiddleware` then writes `ApplicationAccessMessages.NotAuthorisedForTenant` to TempData, signs out the local cookie only (not the IdP) and redirects to `/Error/Forbidden`, which hides the dashboard link for this case. The page filter does the same if a page call hits it. Keep the message free of "token", "expired" and "unauthorized" (American spelling), or the 403 handlers treat it as a token failure and log the user out. `TenantMembershipDenial.ErrorId` must equal Api.Client `TokenExchangeErrors.NoTenantMembershipErrorId`.

## Roles in UI

`Security/AdminAccessHelper.cs`: SuperAdmin, Admin, User, plus Manage claims (`Template:Any:Manage`, …). Policies wrap those for admin folders.

## Page access golden file

**Verified.** `src/Tests/GovUK.Dfe.FlexForms.Web.UnitTests/Security/ExpectedPageSecurity.json` lists the effective access rule for every Razor page and controller action, the way the API's `ExpectedSecurity.json` does for its endpoints. `PageSecurityTests` works it out from `PageAccessConventions` (folder and page rules) plus the attributes on the page, its PageModel, or the controller and action, and fails on any difference, including a new page or action with no row.

- Values: `Anonymous` (anything allowing anonymous access wins over every policy), `None` (nothing protects it; no current row), or the requirements joined with ` + `, all of which apply: `Policy:X`, `Roles:X`, `SignedIn` (a bare `[Authorize]`).
- The file is the Production view. Outside Production only `/TestError` becomes anonymous, and a separate test pins that.
- Change a rule in `PageAccessConventions` or on the page, then update the row in the same PR so the change is visible in review.
- `FrontHubTicketController` is `[AllowAnonymous]` on purpose: `notifications.js` calls it on anonymous pages too, and a signed-out caller gets a 401 from Api.Client without reaching the API.
- `/Shared/PrivacyNotice` and `/Shared/TermsAndConditions` need sign-in today (no anonymous rule).

## Tests for security code

**Verified.** These are unit tested and counted in Sonar coverage; don't add `[ExcludeFromCodeCoverage]` to them or a Sonar exclusion for them:

- `AdminAccessHelperTests`: one theory per check, one row per kind of user (anonymous, User, Caseworker, Template Manager, User Manager, Admin, SuperAdmin). A changed row is a change to who can do what, so call it out in review.
- `ApplicationPermissionHelperTests`: write needs Admin or an exact `Application:{id}:Write` (`Application:Any:Write` doesn't count); read accepts exact or `Any`; starting an application needs Template Write (`Manage` doesn't count).
- `BaseFormPageModelTests`: who can edit an application in which status, using the real `ApplicationStateService.IsApplicationEditable`. A service-authenticated caller with an Admin role is not an admin.
- `TenantSessionBindingMiddlewareTests` and `PermissionsCacheMiddlewareTests`: the tenant mismatch and no-membership paths, and which failures log out (each of the five keywords in `IsAuthenticationFailure`). The `HttpRequestException` and `OperationCanceledException` catches are reached only when the claims transformation throws, because `RefreshAsync` swallows those itself; the tests throw from a substitute transformation.
- `TestAuthenticationHandlerTests`: a Test Authentication session bound to another tenant fails and clears the session; no tenant claim or no resolved tenant is let through.
- `ActivityBasedTokenRefreshMiddlewareTests`: absolute and idle timeouts, refresh failure near expiry, the skip paths, and that Test Authentication bypasses the checks only where it's allowed.
- `TenantConfigurationMiddlewareTests`: 400 for an unresolved public host or a hostname conflict, 404 for a private or Container Apps host, and the bypassed paths.
- `AdminAreaAuthorizationResultHandlerTests`: a signed-in user forbidden on `/admin` gets 404; a signed-out user gets the normal challenge or forbid.
- `UserPermissionsCacheTests`: the claim rebuild, cache key per tenant, and which API failures are rethrown.

`dotnet-ci.yml` still excludes three middleware files by name, and `Authentication/InternalServiceAuthenticationHandler.cs`. Only `MiddlewareExtensions` is untestable wiring. `HostTemplateMiddleware`, `TokenExpiryMiddleware` (not registered) and `InternalServiceAuthenticationHandler` have no tests yet; take a file off the list when it gets tests. New middleware and authentication handlers are counted by default.

## Secrets UX

`TenantSettingSecretPlaintextGate` + CSS masking. Masking is **not** authorization. Production SuperAdmin should see `__REDACTED__` and use reveal.

## Telemetry pseudonymisation

**Verified** in `Telemetry/ReportingUserIdGenerator.cs`. Application Insights user ids are HMAC-SHA256 of tenant id + `NameIdentifier` with host secret `Telemetry:ReportingUserIdKey` (Key Vault → Container App secret or API host-config, ≥32 chars, different per environment). They are pseudonymous personal data. The request log-scope `UserId` (`RequestTelemetryEnrichmentMiddleware`) uses the same id; don't put the raw `NameIdentifier` back in log scopes or `{UserId}` message properties. Missing key → no user id at all; never fall back to a raw id. Rotating the key changes every reporting id. Details: [`../docs/application-insights-reporting.md`](../docs/application-insights-reporting.md).

## Data Protection

`SharedDataProtectionExtensions` — Azure Blob + Key Vault in deployed envs. Blob name must stay **`web-keys.xml`**, distinct from the API ring. Mismatch → `CookieProtection.Unprotect` failures and forced re-login.
