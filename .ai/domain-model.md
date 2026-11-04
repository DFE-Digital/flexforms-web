# Domain model (form engine)

This Domain layer models **templates**, not EA aggregates (those live in the API).

## Types

Under `Domain/Models/`: `FormTemplate`, `Task`, `Page`, `Field`, conditional-logic types, collection/derived flow configuration (`MultiCollectionFlowConfiguration`, `DerivedCollectionFlowConfiguration`).

`Domain/FormEngine/`: `FormRouteParser`, `FormStepPolicy`, `CheckboxValueNormalizer`.

Session keys: `Domain/Caching/FormSessionKeys.cs` — includes `CurrentAccumulatedApplicationId`. Accumulated answers historically used a **single session key** for the browser session, not per application.

## Response envelope

Web transforms UI state to API JSON (`question`, `dataType`, `value`, `completed`). Collection-flow values are nested JSON. Prism flattening comments in `flexforms-prism` `ResponseParser` claim alignment with Web `TransformToResponseJson`.

Transport: Web may Base64-encode bodies; API stores JSON. Load errors on a row are usually **bad payload**, not a missing decode-on-read (**Historical** GitHub #59).

## Template Studio

Client-side editor in `wwwroot/js/template-studio/` mutates the same JSON the engine consumes. Raw JSON tab is advanced mode.
