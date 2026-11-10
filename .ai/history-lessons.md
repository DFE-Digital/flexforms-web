# History lessons (Web)

Lessons from past engineering work. Check the code before relying on **Historical** or **Unverified** items.

| Lesson | Status |
|--------|--------|
| Full-snapshot saves + a shared session key caused data loss; hotfix added reload + stale-session guard; long-term patch-save is not shipped | **Verified** guard; **Unverified** that patch-save exists |
| A Redis connect timeout took down Web (session) while the API degraded; health probes hit Redis via telemetry | **Verified** — health endpoints use `ShortCircuit()` |
| `TokenRefresh:TokenEndpoint` is required for Entra-only tenants | **Verified** — `TenantTokenRefreshOptionsAccessor` |
| `returnToSummaryPage` still goes to a newly visible next page | **Verified** in `SaveFormPageService` behaviour |
| OTP: API generates/verifies, Web mints the JWT | **Verified** |
| Template Studio FeatureManagement uses Target `Web` | **Verified** |
| UI secret masking is not access control | **Verified** |
| Autocomplete must debounce and cancel in-flight requests | **Historical** — check current `_AutocompleteComplexField` |
| `applicationMetadata` fieldIds should validate on template save | **Historical** |
| Template save requires a reporting-impact acknowledgement | **Verified** in Template Manager |
| `docs/FlexForms-Process-Map.md` is the shareable end-to-end narrative | **Verified** |
| Organisation settings saved on one Web replica stayed stale on the others for up to 10 minutes, because cache invalidation was in-process only; fixed with a Redis pub/sub broadcast | **Verified** — `TenantConfigurationInvalidator` |
| A template version saved on one replica stayed old on the others for up to 24 hours (in-memory template cache, local-only `Remove`). Fixed with a two-level cache (60 s memory + Redis, pub/sub to drop memory copies) so reads stay fast and saves show everywhere at once; Redis-only was considered but made every template read a ~190 KB Redis GET + parse. Tenant config stayed memory-only with a pub/sub broadcast because it holds secrets and must survive Redis outages | **Verified** — `TwoLevelCache`, `FormTemplateProvider`, `ApplicationStatusService` |

Collection-flow saves (multi and derived) are in the same risk class as file delete: they build from the session snapshot. Re-read `DerivedCollectionFlowService` and `SaveFormPageService` before changing them.
