# Integrations (Web)

## flexforms-api

Typed clients from `AddExternalApplicationsApiClients`: applications, templates, users, roles, notifications, tenant admin, tokens, hub auth, feedback, reporting export, application owner.

All business calls: exchanged JWT + `X-Tenant-ID` + `x-correlationId` (+ optional `X-Template-Id` / `X-Application-Reference`).

Startup: app-only Entra token → host-config.

## SignalR

Browser connects to **API** `{api-base}/hubs/notifications`, not Web. Ticket via `FrontHubTicketController` → `IHubAuthClient`. Cypress sets `window.Cypress` and skips the hub (WebSocket proxy issues).

## Scan results

`Infrastructure/Consumers/ScanResultConsumer.cs` listens to `file-scanner-results`. Handles infected UX (blacklist, strip from session JSON, notify). API consumer deletes storage rows. **Keep both.** Local instance-id filter applies.

## Autocomplete

`IAutocompleteService` + accessible-autocomplete. External establishment APIs (including Academies-style filters `IncludeOnlyAcademies`). Debounce/cancel in-flight requests — historical bug: min-3-chars fired a request per keystroke.

## GOV.UK Frontend

`GovUk.Frontend.AspNetCore` 4.x — rebrand always on (`Program.cs` comment).

## MassTransit

Configured in bootstrap. Web is a **consumer** of scan results, not the scan publisher.
