# Integrations (Web)

## flexforms-api

Typed clients from `AddExternalApplicationsApiClients`: applications, templates, users, roles, notifications, tenant admin, tokens, hub auth, feedback, reporting export, application owner.

All business calls: exchanged JWT + `X-Tenant-ID` + `x-correlationId` (+ optional `X-Template-Id` / `X-Application-Reference`).

Startup: app-only Entra token → host-config.

## SignalR

Browser connects to **API** `{api-base}/hubs/notifications`, not Web. Ticket via `FrontHubTicketController` → `IHubAuthClient`. Cypress sets `window.Cypress` and skips the hub (WebSocket proxy issues).

Cookie lifecycle in `wwwroot/js/notifications.js` (**Verified**): the API ticket is single use for 60s and the `hubauth` cookie lasts 10 minutes. `ensureHubCookie` treats a failed redeem as failure (30s cooldown). If the first start fails (expired session, ticket redeemed too late) or the connection closes after reconnect gives up, `scheduleHubStartRetry` resets `__flexformsHubStarted` and retries with exponential backoff (30s doubling, capped at 10 minutes), only while the tab is visible; a `visibilitychange` back to visible also restarts it. Renewal of a live connection's cookie runs every 8 minutes only while the tab is visible, on `visibilitychange` when the cookie is stale, and on `onreconnecting` so the retry negotiate has a live cookie. The renewal timer and visibility listener are registered once at module level, so retries never stack them. `FrontHubTicketController` returns the API's 401/403 as-is (information log, no exception) when the user's API session has expired.

`/notifications/unread` 403 means the user's API claims have no `Notifications:{email}:Read` for this tenant. It is an API grant problem, not Web (see flexforms-api `.ai/features/notifications-signalr.md`).

## File validation messages (Markdown)

Tenant functions post `message` to the API's file-validation callback; the API stores it raw and it may be Markdown (**Verified**). Web renders it with `Utilities/MarkdownSafe.RenderIntegrationMessage`: Markdig with raw HTML disabled, then an HtmlSanitizer allow-list of `p`, `br`, `strong`, `em`, `code`, `ul`, `ol`, `li` and https `a`, plus GOV.UK classes. No headings. A single paragraph comes back `isInline` so it follows the status label after " — ".

- Server-rendered: upload Status column (`Views/Shared/Fields/_UploadComplexField.cshtml`) and the preview submit check (`Views/Shared/FormEngine/_ApplicationPreview.cshtml`).
- Live (SignalR): `notifications.js` shows the text first, then POSTs it to `FileValidationMessageController` (`/file-validation/message/render`, `[Authorize]` + antiforgery) and swaps in the returned HTML. This covers the Status cell and the file-validation banner. The banner splits the API text into the fixed lead and the detail by matching `metadata.message` at the end of `notification.message`.
- Not rendered: the `/Notifications` list page still shows plain text.

HtmlSanitizer removes disallowed elements **with their text**. That's why `code` is allowed here; adding Markdown features means adding their tags to `SanitizerIntegrationMessage`, or the text disappears.

## Scan results

`Infrastructure/Consumers/ScanResultConsumer.cs` listens to `file-scanner-results`. Handles infected UX (blacklist, strip from session JSON, notify). API consumer deletes storage rows. **Keep both.** Local instance-id filter applies.

## Autocomplete

`IAutocompleteService` + accessible-autocomplete. External establishment APIs (including Academies-style filters `IncludeOnlyAcademies`). Debounce/cancel in-flight requests — historical bug: min-3-chars fired a request per keystroke.

Enter key behaviour lives in `_AutocompleteComplexField.cshtml`: while suggestions are open, Enter is intercepted so it never submits the form. With no arrow-highlighted option it selects the first suggestion. See [`known-pitfalls.md`](known-pitfalls.md) #17.

## GOV.UK Frontend

`GovUk.Frontend.AspNetCore` 4.x — rebrand always on (`Program.cs` comment).

## MassTransit

Configured in bootstrap. Web is a **consumer** of scan results, not the scan publisher.

## Application Insights (usage reporting)

**Verified.** SDK 2.23 (`AddApplicationInsightsTelemetry`), `TenantAwareTelemetryChannel` for per-tenant resources, processors `HealthCheckTelemetryFilter` (drops probes) and `ClientAbortedRequestTelemetryProcessor`, and Serilog → `ExceptionTrackingTelemetryConverter` for traces/exceptions. Adaptive sampling is the SDK default (not overridden in repo config).

`Telemetry/ReportingTelemetryInitializer.cs` (registered in `Program.cs` after `AddApplicationInsightsTelemetry`, so the SDK's `ai_session`/`ai_user`/synthetic initializers run first) adds:

- `User.AuthenticatedUserId` + `ReportingUserId`: tenant-scoped HMAC-SHA256 of `NameIdentifier` (`ReportingUserIdGenerator`, key `Telemetry:ReportingUserIdKey`). No key, tenant or `NameIdentifier` → no id, never the raw value.
- `SessionId` (and `Session.Id` when the browser `ai_session` did not set it): hash of the ASP.NET session's internal id + `UserActivityTracker` `Session:StartTime`, bound by `RequestTelemetryEnrichmentMiddleware` inside its existing session-cookie check.
- `TenantId` (bound by `TenantConfigurationMiddleware`), `UserType` (existing roles / `service_type=internal`), `ServiceName`, and on requests `IsAuthenticated`, `IsHealthCheck`, `IsStaticResource`, `IsSyntheticTraffic` (true only when known).

`RequestTelemetryEnrichmentMiddleware` puts the same reporting id (not the raw `NameIdentifier`) in the log-scope `UserId`, so traces and exceptions match `user_AuthenticatedId`; `UserEmail` stays masked. Log messages about the signed-in user use `ReportingUserIdGenerator.ForLogging` for `{UserId}`.

The initializer never overwrites existing values and reads only `HttpContext.User`, request path/headers and `ReportingTelemetryItems` (`HttpContext.Items`). Reporting guide and KQL: [`../docs/application-insights-reporting.md`](../docs/application-insights-reporting.md).
