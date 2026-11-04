# Feature: Form engine

## 1. Purpose

Render template tasks/pages/fields with GOV.UK components, conditional logic, collection and derived flows, save/complete/submit.

## 2. Entry points

- `Pages/FormEngine/RenderForm.cshtml.cs` (+ `RenderForm.Support.cs`)
- Application: `IPrepareFormEngineGet`, `ISaveFormPage`, `ICompleteFormTask`, `ISubmitFormApplication`, file use cases
- Infrastructure: `FormStateManager`, `ConditionalLogicEngine`, `ConditionalLogicOrchestrator`, `DerivedCollectionFlowService`
- Domain: `FormRouteParser`, `FormStepPolicy`
- Field partials: `Views/Shared/Fields/_*.cshtml`

## 3. Flow

GET prepare → render field partials. POST save → validate → merge into accumulated JSON → API add-response (full body) → navigate (standard / collection / derived). Complete task / submit call API.

## 4. State

Session accumulated data + `CurrentAccumulatedApplicationId`. API is source of truth; session is a working copy. **Never save session if it belongs to another application.**

## 5. Other repos

API applications/files endpoints. Prism flattens the JSON Web produces.

## 6. Rules

See [`../business-rules.md`](../business-rules.md). Conditional logic from template JSON. Collection items have their own pages; deleting an item must clean associated files.

## 7. Edge cases

- Two-tab contamination.
- Redis miss treated as empty session (must throw).
- `returnToSummaryPage` vs newly visible pages.
- Derived flows vs multi-collection flows are different models — don’t share serializers casually.
- Infected upload filter / Redis infected-file store.
- Upload file limit (**Verified**): `ComplexFieldConfiguration.MaxSelections` on the tenant's upload complex field is the maximum file count (0 = none). `UploadFieldFileLimit` finds the field in the current task/flow first (field ids may repeat across collection and derived flows), then the rest of the task, then the whole template. The current flow comes from `FlowId`, `DerivedFlowId`, or the page route (`{flowId}/derived/{itemId}/{pageId}`), because derived-flow uploads only carry it in the route (`UploadFieldFileLimit.CurrentFlowId`). Enforced in `UploadFormFileService` before the API upload (needs `state.Template`), backstopped in `FormValidationOrchestrator` on save (decided by the configured `FieldType`, not the id), and shown in `_UploadComplexField.cshtml`. The standalone `UploadFile` page saves files straight to the application, so it refuses the upload when the template can't be loaded. `RenderForm` keeps going without it, because its uploads only reach the session and the save-time check still applies.
- Derived-flow uploads share one file list across all items of the flow, so the file limit counts every item's files together. Known gap, see [`../known-pitfalls.md`](../known-pitfalls.md) #14. **Historical:** `MaxSelections` was only read by autocomplete, so upload limits were silently ignored.

## 8. Tests

Application FormEngine tests, Infrastructure conditional-logic tests, Web RenderForm characterization tests, Playwright create-and-submit journeys.

## 9. Limits

Engine is template-driven; product-specific behaviour belongs in JSON + tenant FormEngine settings, not hardcoded Transfers logic. RenderForm was historically huge; keep new logic in services.
