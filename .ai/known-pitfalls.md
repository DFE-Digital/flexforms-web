# Known pitfalls (Web)

1. **`StaleFormSessionException`.** Session answers are not per-application. File delete / upload / remove-item paths used to save without reloading API state. Two tabs → truncated `ResponseBody`. Guard lives in Application + `ExternalApiPageExceptionFilter`. Do not add a new save path that skips the guard.
2. **Health without ShortCircuit** + telemetry reading `Session` → Redis GET on every probe → restart loop when Redis is down.
3. **Single replica + Redis outage** = 100% connection refused. Prefer ≥2 web replicas in prod.
4. **`TokenRefresh:TokenEndpoint` required** even for Entra-only tenants.
5. **Test Auth:** key length; Web Target overriding Shared; per-replica tenant-config cache delay; optional API URL rewrite to another environment.
6. **Data Protection ring** must be shared across web replicas and **not** the API ring.
7. **Forwarded headers** required behind ACA/Front Door or OIDC redirect URIs break.
8. **SignalR/Cypress** — tests disable the hub. Don’t “fix” Cypress by forcing WebSockets without the proxy.
9. **FeatureManagement Target** for Template Studio is `Web`, not `Api`/`Shared`.
10. **Upload summary blank** → FormEngine `AllowMultiple` + refresh settings. The upload **file limit** is `MaxSelections` on the same entry (see `.ai/features/form-engine.md`); it used to be ignored for uploads.
11. **Render notification text with `textContent` or a sanitiser**, never `innerHTML`.
12. **GovUK Frontend 4 rebrand** is always on — don’t fight it.
13. Redis is required for cookies/session; API can degrade to SQL. Same incident, different UX.
14. **Derived-flow uploads are not stored per item** (**Verified**, not yet fixed). Collection flows keep files per instance (`FlowProgress_{flowId}_{instanceId}`), but an upload question on a derived-flow page has no flow context in `FormFileFieldContext`, so `FormFileFieldService.GetFiles`/`SaveFiles` use one session key per application and field (`UploadedFiles_{applicationId}_{fieldId}`). Every item of that derived flow shares one file list, and the upload file limit (`MaxSelections`) counts files from all items together. Fixing it means scoping the storage key, the save path and the summary per derived item. Check whether any live template has an upload question inside a derived flow before relying on per-item files there.
15. **Application Insights host connection string is Web's own.** Web sends telemetry using its own `ApplicationInsights:ConnectionString` (Terraform hosting module sets `ApplicationInsights__ConnectionString` from the module's App Insights resource; don't also put it in `container_secret_environment_variables` or Terraform fails with a duplicate key). The API no longer sends its connection string in host-config. A tenant `ApplicationInsights` setting overrides it per request. Web only reads host-config at startup — restart after API host-config changes.
16. **Custom telemetry channel must forward the ingestion endpoint.** `Telemetry/TenantAwareTelemetryChannel.cs` wraps an inner `ServerTelemetryChannel`. The SDK applies the connection string's `IngestionEndpoint` only to the registered wrapper, so the inner host channel posted to the legacy global endpoint: browser page views arrived but server requests/traces did not. `Initialize` now sets the inner endpoint from `configuration.EndpointContainer.Ingestion`. Tenant channels were never affected. Same fix in flexforms-api.
