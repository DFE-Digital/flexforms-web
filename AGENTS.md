# FlexForms Web — Agent entry point

Razor Pages frontend for **FlexForms**. Turns JSON templates into GOV.UK task-list applications. Persistence and business rules live in **flexforms-api**. This repo owns UI, cookie sessions, form orchestration, admin tools, and a `ScanResultConsumer` for malware UX.

Start here, then open only the `.ai/` files that match the task.

## Ecosystem

Companion of `flexforms-api`. Reporting is `flexforms-prism` (no code dependency). Virus scan results arrive on Service Bus; files are uploaded **through the API**. Shared libraries: `DfE.CoreLibs` (NuGet) and `GovUK.Dfe.FlexForms.Api.Client`.

## Constraints

1. **Clean Architecture.** PageModels must not reference Infrastructure. Application must not take `HttpContext` / `ISession`. Enforced by `CleanArchitectureGuardTests`.
2. **Use-case pattern.** PageModel: `CaptureWorkState()` → Application service → `ApplyWorkState()` → `MapOutcome()`. Do not put business logic in PageModels.
3. **`PlatformBootstrap:Enabled` must be true.** Legacy `configurations/{APPLICATION_NAME}/` is unused. Startup throws otherwise.
4. **Do not save form JSON from session without reloading the application** (or hitting `StaleFormSessionException`). Cross-tab session key `AccumulatedFormData` caused production data loss.
5. **Health endpoints must stay `.ShortCircuit()`.** Probes that touch session/Redis restart the container during Redis blips.
6. **Separate Data Protection blob** (`web-keys.xml`) from the API ring. Mixing them, or losing the ring, forces re-login / breaks cookies.
7. **Redis session must fail loud** (`SessionDistributedCacheAdapter`). Never treat a Redis error as an empty session.
8. **CoreLibs via NuGet** in CI. Local Api.Client project reference is optional and gitignored (`Directory.Build.targets.user`).
9. Entra-only tenants still need `TokenRefresh:TokenEndpoint` (or DfESignIn Authority). Empty TokenEndpoint crashes the site.
10. Feature flags for this app (e.g. Template Studio) are TenantSettings **Target `Web`**.

## How to understand a change

1. Read [`README.md`](README.md) architecture section if you are new.
2. Open the matching [`.ai/features/`](.ai/features/) file and [`.ai/known-pitfalls.md`](.ai/known-pitfalls.md).
3. Confirm against PageModels + Application services. The form engine is large — `RenderForm` is a dispatcher, logic is in Application/Infrastructure services.

## Knowledge map

| File | Use when |
|------|----------|
| [`.ai/README.md`](.ai/README.md) | Index |
| [`.ai/architecture.md`](.ai/architecture.md) | Layers, middleware, bootstrap |
| [`.ai/domain-model.md`](.ai/domain-model.md) | FormTemplate models, routing |
| [`.ai/business-rules.md`](.ai/business-rules.md) | Navigation, live templates, contributors |
| [`.ai/security.md`](.ai/security.md) | OIDC/Entra/test auth, cookies |
| [`.ai/integrations.md`](.ai/integrations.md) | API client, SignalR, scan consumer |
| [`.ai/dependencies.md`](.ai/dependencies.md) | Packages, other repos |
| [`.ai/development-workflows.md`](.ai/development-workflows.md) | Local run, tests, release |
| [`.ai/known-pitfalls.md`](.ai/known-pitfalls.md) | Incidents |
| [`.ai/history-lessons.md`](.ai/history-lessons.md) | Lessons from past incidents |
| [`.ai/decisions/`](.ai/decisions/) | ADRs (few in this repo; many live in API) |
| [`.ai/features/`](.ai/features/) | Form engine, bootstrap, admin, Template Studio |

Human manuals: `docs/FlexForms-Process-Map.md`, `docs/Form-Template-Designer-Manual.md`, `docs/Template-Studio-User-Manual.md`, `docs/Tenant-Admin-User-Manual.md`. Prefer updating those for operator UX; keep `.ai/` for implementation.

## Knowledge maintenance

Read relevant `.ai/` files before large changes. Trust code over docs, and fix the docs when they disagree. Label knowledge **Verified** / **Historical** / **Unverified**. Never silently overwrite verified knowledge with speculation.

### Definition of done (mandatory)

This is enforced in CI: the **Knowledge base check** job fails a pull request that changes production code under `src/` (tests excluded) without touching `AGENTS.md` or `.ai/`, unless the PR has the `no-docs` label. It runs first; the build only starts once it passes. After adding the label, re-run the failed job.

A task is **not complete** until you have checked whether it changed anything the knowledge base describes. Before finishing:

1. Check the change against the triggers below.
2. Update every affected `.ai/` file in the same change, citing the source files you touched.
3. In your final summary or PR description, list the `.ai/` files you updated, **or** state “No knowledge impact” with a one-line reason.

| If you changed… | Update |
|-----------------|--------|
| Form save, navigation, collection/derived flows, session handling | `.ai/features/form-engine.md`, `.ai/business-rules.md`, `.ai/known-pitfalls.md` |
| Auth schemes, token refresh, tenant resolution, bootstrap | `.ai/security.md`, `.ai/features/auth-and-bootstrap.md` |
| Admin pages, Template Studio, feature flags | `.ai/features/admin-and-studio.md` |
| Middleware order, layers, DI composition | `.ai/architecture.md` |
| API client usage, SignalR, scan consumer | `.ai/integrations.md` — and flag any flexforms-api change needed |
| Template JSON shape or response envelope | `.ai/domain-model.md` — and flag flexforms-prism flattening + the designer manual |
| Local setup, tests, CI or release steps | `.ai/development-workflows.md` |
| A fix for an incident or footgun | `.ai/known-pitfalls.md` (and `.ai/history-lessons.md` if worth keeping) |
| A significant design choice | New file in `.ai/decisions/` |
| Something that makes an existing `.ai/` statement wrong | Correct or delete it — never leave it stale |

Operator-facing behaviour changes also need the relevant manual in `docs/` updated.

This repository is **public**. Never write secrets, credentials, tenant secret values, or descriptions of unfixed security weaknesses into `.ai/` or `AGENTS.md`. Record the rule, not the vulnerability.
