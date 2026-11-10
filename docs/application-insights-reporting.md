# Application Insights: user and session reporting

How FlexForms Web labels its server-side Application Insights telemetry so the reporting team can count authenticated usage without seeing who the users are.

Code: `src/GovUK.Dfe.FlexForms.Web/Telemetry/ReportingTelemetryInitializer.cs`, `ReportingUserIdGenerator.cs` and `ReportingTelemetryItems.cs`.

## Where the data lands

Web sends telemetry to its own Application Insights resource. A tenant that has its own `ApplicationInsights` tenant setting gets its request telemetry in **that tenant's** resource instead (`TenantAwareTelemetryChannel`). To report across all tenants, query every resource involved.

Only server telemetry is enriched. The browser JavaScript SDK loads only after the user accepts analytics cookies, and it is unchanged.

## Fields

| Field (KQL column) | Telemetry | Value |
|---|---|---|
| `user_AuthenticatedId` | Requests, dependencies, exceptions and traces during a signed-in request | The pseudonymous reporting id (see below). Set only if nothing else has set it. |
| `user_Id` | All | Unchanged: the anonymous browser `ai_user` cookie, only when analytics cookies were accepted. |
| `session_Id` | All | The browser `ai_session` id when analytics cookies were accepted, otherwise the server session id below. |
| `customDimensions.ReportingUserId` | As `user_AuthenticatedId` | Same value as `user_AuthenticatedId`. |
| `customDimensions.UserId` / `UserEmail` | Traces and exceptions (request log scope) | `UserId` is the same reporting id. `UserEmail` stays masked as before (`PiiMasking`). |
| `customDimensions.SessionId` | Requests with a session cookie, and their dependencies, exceptions and traces | The server session id. It is on every session, whatever the cookie choice, so **use this column to count sessions**. |
| `customDimensions.TenantId` | Everything after the tenant has been resolved | The tenant GUID (already used in FlexForms logs). |
| `customDimensions.UserType` | Signed-in requests | `InternalService` (service account, not a person), `SuperAdmin`, `Admin` or `User` (existing roles). Empty for users who only have custom roles. |
| `customDimensions.IsAuthenticated` | Requests | `true` / `false`: whether the request carried a signed-in user. |
| `customDimensions.IsHealthCheck` | Requests | `true` for `/health`, `/healthz`, `/liveness`, `/readiness`. These requests are dropped before they are sent (`HealthCheckTelemetryFilter`), so you should not see any. |
| `customDimensions.IsStaticResource` | Requests | `true` for CSS, JavaScript, images, fonts and GOV.UK Frontend assets. |
| `customDimensions.IsSyntheticTraffic` | Requests | `true` only for Application Insights availability tests (`operation_SyntheticSource`). If it is missing, the request is not known to be synthetic. It is **not** proof that a person made it. |
| `customDimensions.ServiceName` | All | `flexforms-web`. |
| `customDimensions.AspNetCoreEnvironment` | All | The environment name, already added by the SDK. FlexForms does not add a separate `Environment` field. |

The user's name, email address, sign-in claims and identity-provider id are never put in these fields. Form answers, application contents and query strings are not added either.

## Pseudonymous user id

`ReportingUserId = first 16 bytes, as hex, of HMAC-SHA256(key, "flexforms-web/reporting-user/v1|{tenantId}|{NameIdentifier}")`

- The input is the `NameIdentifier` claim, the same immutable id Web already uses for permissions and ownership checks (the DfE Sign-In or Entra subject).
- The same person in the same tenant always gets the same id, on any device and in any session.
- Tenants are part of the input, so one person gets unrelated ids in different tenants. Reports cannot follow a user from one tenant to another.
- No id is added if the request has no resolved tenant, no `NameIdentifier`, or the key is not configured.

## Sessions

`customDimensions.SessionId` is a SHA-256 hash of two values: the ASP.NET Core session's internal id (not the cookie value) and the sign-in start time that `UserActivityTracker` already keeps in the session. Web adds no cookie or storage for it.

A new session id starts when:

- the user signs in again after an idle timeout (30 minutes), the absolute timeout (8 hours), or signing out. All three clear the session.
- the ASP.NET session itself ends: after 45 minutes without a request, or when the browser deletes its session cookie (usually when the browser closes).

Limitations:

- This is a server sign-in session, not an exact "visit". Tabs in the same browser share one session.
- Test Authentication tenants do not record a sign-in start time. Their sessions only end when the ASP.NET session ends.
- Pages before sign-in get a session id that changes once the user signs in.
- Requests that finish before the telemetry middleware runs have no session id. These are static files, sign-in redirects, errors loading the tenant, and the very first request before a session cookie exists.
- `session_Id` mixes browser sessions (with cookie consent) and server sessions (without it). Count with `customDimensions.SessionId`.

## Sampling

Adaptive sampling is on: it is the SDK default, and `ApplicationInsights:EnableAdaptiveSampling` is not set in this repository (check the deployed environment variables before relying on this). Each replica keeps roughly 5 telemetry items per second and drops the rest. It keeps or drops a request together with its dependencies, exceptions and traces. Each kept row's `itemCount` says how many rows it stands for.

- **Total requests:** use `sum(itemCount)`, not `count()`. That gives an estimate, not an exact figure.
- **Unique users and sessions:** `dcount` only sees users and sessions that had at least one kept request. Under heavy sampling this **under-counts** light users. `dcount` is also an approximation in itself. Present these figures as estimates.
- **Activity per user:** `sum(itemCount)` per user is an estimate; for users with few requests it can be well off.

If the reporting team needs exact counts, exclude `Request` from adaptive sampling (`ExcludedTypes`) as a separate, costed configuration change. That decision is outside this enrichment.

## Excluding automated traffic

Recommended filter for "people using FlexForms":

```kusto
let UserTraffic = requests
    | where tostring(customDimensions.IsHealthCheck) != "true"
    | where tostring(customDimensions.IsStaticResource) != "true"
    | where tostring(customDimensions.IsSyntheticTraffic) != "true" and isempty(operation_SyntheticSource)
    | where tostring(customDimensions.UserType) != "InternalService";
```

Add `| where tostring(customDimensions.IsAuthenticated) == "true"` for signed-in traffic only. Rows sent before this change have no such fields. The `!= "true"` checks keep them, so restrict the time range to dates after deployment.

Automated tests that sign in through Test Authentication look like normal users. Exclude test tenants by `TenantId` if needed.

## Example queries

Unique authenticated users per day:

```kusto
requests
| where timestamp > ago(30d)
| where isnotempty(user_AuthenticatedId)
| where tostring(customDimensions.IsStaticResource) != "true"
| where isempty(operation_SyntheticSource)
| where tostring(customDimensions.UserType) != "InternalService"
| summarize Users = dcount(user_AuthenticatedId, 4) by bin(timestamp, 1d)
| render timechart
```

Requests per pseudonymous user:

```kusto
requests
| where timestamp > ago(7d)
| where isnotempty(user_AuthenticatedId)
| where tostring(customDimensions.IsStaticResource) != "true"
| summarize Requests = sum(itemCount), Sessions = dcount(tostring(customDimensions.SessionId))
    by user_AuthenticatedId, UserType = tostring(customDimensions.UserType)
| order by Requests desc
```

Usage by tenant:

```kusto
requests
| where timestamp > ago(30d)
| where isnotempty(user_AuthenticatedId)
| where tostring(customDimensions.IsStaticResource) != "true"
| where tostring(customDimensions.UserType) != "InternalService"
| summarize Requests = sum(itemCount), Users = dcount(user_AuthenticatedId, 4)
    by TenantId = tostring(customDimensions.TenantId)
| order by Requests desc
```

Sessions per tenant (server sign-in sessions as described above, not visits):

```kusto
requests
| where timestamp > ago(30d)
| where tostring(customDimensions.IsAuthenticated) == "true"
| where tostring(customDimensions.UserType) != "InternalService"
| extend TenantId = tostring(customDimensions.TenantId), SessionId = tostring(customDimensions.SessionId)
| where isnotempty(SessionId)
| summarize Sessions = dcount(SessionId, 4), Users = dcount(user_AuthenticatedId, 4) by TenantId
```

Signed-in vs anonymous vs automated requests:

```kusto
requests
| where timestamp > ago(7d)
| extend Kind = case(
    tostring(customDimensions.IsStaticResource) == "true", "Static",
    tostring(customDimensions.IsSyntheticTraffic) == "true", "Synthetic",
    tostring(customDimensions.UserType) == "InternalService", "Service account",
    tostring(customDimensions.IsAuthenticated) == "true", "Signed in",
    "Anonymous")
| summarize Requests = sum(itemCount) by Kind
```

## Privacy and key management

- `ReportingUserId` is **pseudonymous personal data** under UK GDPR, because anyone holding the key and a user's id can recompute it. Treat Application Insights exports and saved queries accordingly. Access stays under the existing Application Insights RBAC.
- The key is host configuration `Telemetry:ReportingUserIdKey` (environment variable `Telemetry__ReportingUserIdKey`). Supply it from Key Vault as a Container App secret (`container_secret_environment_variables`) or through API host-config. Never put it in `appsettings*.json` or tenant settings.
- The key must be at least 32 characters of random data, for example `openssl rand -base64 48`. Use a different key in each environment so ids cannot be matched across environments.
- If the key is missing or too short, Web logs one line at startup and sends no user ids. It never falls back to the raw id. Requests are not affected.
- Web reads the key once at startup. Telemetry never calls Key Vault, and the key is never logged.
- **Rotating the key changes every user's id.** Users before and after the rotation cannot be joined, and unique-user counts that span the rotation date are inflated. Rotate only when necessary (for example if the key is exposed), and record the date for the reporting team.
- For a subject access or erasure request, recompute the user's id with the current key and find their rows that way.
- Server telemetry adds no cookies. The session id is derived from the existing essential session cookie. The browser `ai_user` and `ai_session` cookies exist only after analytics consent. Confirm with the data protection lead that the privacy notice covers usage reporting.
