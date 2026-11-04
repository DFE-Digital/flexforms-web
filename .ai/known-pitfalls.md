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
10. **Upload summary blank** → FormEngine `AllowMultiple` + refresh settings.
11. **Render notification text with `textContent` or a sanitiser**, never `innerHTML`.
12. **GovUK Frontend 4 rebrand** is always on — don’t fight it.
13. Redis is required for cookies/session; API can degrade to SQL. Same incident, different UX.
