# Domain model (form engine)

This Domain layer models **templates**, not EA aggregates (those live in the API).

## Types

Under `Domain/Models/`: `FormTemplate`, `Task`, `Page`, `Field`, conditional-logic types, collection/derived flow configuration (`MultiCollectionFlowConfiguration`, `DerivedCollectionFlowConfiguration`).

`ComplexField.Label` (**Verified**) is an optional sentence name for one search question, stored as `complexField.label`. Blank keeps the tenant `FormEngine:ComplexFields` Label. Summary item labels use it as written. `ComplexField.ConfirmationTitle` (**Verified**) is an optional confirmation heading, stored as `confirmationTitle`. `{name}` is replaced with that sentence name. Blank keeps "Is this the right {name}?".

`Domain/FormEngine/`: `FormRouteParser`, `FormStepPolicy`, `CheckboxValueNormalizer`.

Session keys: `Domain/Caching/FormSessionKeys.cs` — includes `CurrentAccumulatedApplicationId`. Accumulated answers historically used a **single session key** for the browser session, not per application.

## Response envelope

Web transforms UI state to API JSON (`question`, `dataType`, `value`, `completed`). Collection-flow values are nested JSON. Prism flattening comments in `flexforms-prism` `ResponseParser` claim alignment with Web `TransformToResponseJson`.

Transport: saves (`ApplicationResponseService`) send Base64 and the API stores decoded JSON. Create sends raw `"{}"` and the API stores `InitialResponseBody` verbatim, so a caller that Base64-encodes on create (the Playwright `ApplicationBuilder` used to, `e30=`) leaves an encoded body until the first save. Every reader must accept both: `DashboardAnswerReader.DecodeResponseBody`, `ApplicationStatusService`, and `ApplicationStateService.LoadResponseDataIntoSessionAsync`. The last one used to parse raw only and logged `JsonReaderException: 'e' is an invalid start of a value` for those applications (**Verified**). Other load errors on a row are usually **bad payload** (**Historical** GitHub #59).

## Template Studio

Client-side editor in `wwwroot/js/template-studio/` mutates the same JSON the engine consumes. Raw JSON tab is advanced mode.
