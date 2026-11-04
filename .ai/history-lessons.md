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

Collection-flow saves (multi and derived) are in the same risk class as file delete: they build from the session snapshot. Re-read `DerivedCollectionFlowService` and `SaveFormPageService` before changing them.
