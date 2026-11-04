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
