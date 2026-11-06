# Integrations (Web)

## flexforms-api

Typed clients from `AddExternalApplicationsApiClients`: applications, templates, users, roles, notifications, tenant admin, tokens, hub auth, feedback, reporting export, application owner.

All business calls: exchanged JWT + `X-Tenant-ID` + `x-correlationId` (+ optional `X-Template-Id` / `X-Application-Reference`).

Startup: app-only Entra token → host-config.

## SignalR

Browser connects to **API** `{api-base}/hubs/notifications`, not Web. Ticket via `FrontHubTicketController` → `IHubAuthClient`. Cypress sets `window.Cypress` and skips the hub (WebSocket proxy issues).

Cookie lifecycle in `wwwroot/js/notifications.js` (**Verified**): the API ticket is single use for 60s and the `hubauth` cookie lasts 10 minutes. `ensureHubCookie` treats a failed redeem as failure (30s cooldown). If the first start fails (expired session, ticket redeemed too late) or the connection closes after reconnect gives up, `scheduleHubStartRetry` resets `__flexformsHubStarted` and retries with exponential backoff (30s doubling, capped at 10 minutes), only while the tab is visible; a `visibilitychange` back to visible also restarts it. Renewal of a live connection's cookie runs every 8 minutes only while the tab is visible, on `visibilitychange` when the cookie is stale, and on `onreconnecting` so the retry negotiate has a live cookie. The renewal timer and visibility listener are registered once at module level, so retries never stack them. `FrontHubTicketController` returns the API's 401/403 as-is (information log, no exception) when the user's API session has expired.

`/notifications/unread` 403 means the user's API claims have no `Notifications:{email}:Read` for this tenant. It is an API grant problem, not Web (see flexforms-api `.ai/features/notifications-signalr.md`).

## Scan results

`Infrastructure/Consumers/ScanResultConsumer.cs` listens to `file-scanner-results`. Handles infected UX (blacklist, strip from session JSON, notify). API consumer deletes storage rows. **Keep both.** Local instance-id filter applies.

## Autocomplete

`IAutocompleteService` + accessible-autocomplete. External establishment APIs (including Academies-style filters `IncludeOnlyAcademies`). Debounce/cancel in-flight requests — historical bug: min-3-chars fired a request per keystroke.

Enter key behaviour lives in `_AutocompleteComplexField.cshtml`: while suggestions are open, Enter is intercepted so it never submits the form. With no arrow-highlighted option it selects the first suggestion. See [`known-pitfalls.md`](known-pitfalls.md) #17.

## GOV.UK Frontend

`GovUk.Frontend.AspNetCore` 4.x — rebrand always on (`Program.cs` comment).

## MassTransit

Configured in bootstrap. Web is a **consumer** of scan results, not the scan publisher.
