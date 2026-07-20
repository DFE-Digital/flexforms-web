# Apply to transfer an academy – External Applications Web

This repository contains the Razor Pages frontend for submitting applications to transfer academies into another trust. It uses a Clean Architecture layout (Domain ➜ Application ➜ Infrastructure ➜ Web) and drives every screen from JSON form templates delivered by the External Applications API. Template-driven pages, conditional logic, file uploads, and contributor management are all orchestrated in the web layer while persistence and validation live in the API.

## Features
- 🧩 Template-driven form engine: tasks/pages/fields come from template schemas fetched via `ITemplatesClient` and parsed into domain models.
- 🔀 Dynamic logic: conditional visibility/requirements, collection flows, and derived collection flows handled server-side in `RenderForm`.
- 👥 Contributor management: invite/remove contributors before entering the form and keep application state in session + API.
- 🔐 Secure authentication: DfE Sign-in (OIDC) plus optional internal/test schemes; session-based token refresh and permissions caching.
- 🛡️ File uploads with AV protection: files posted to the API, scanned asynchronously, and cleaned/blacklisted when `ScanResultConsumer` receives an infected result.
- 📢 Notifications and events: publishes `TransferApplicationSubmittedEvent` to Azure Service Bus after successful submission; renders API-sourced notifications to users.
- 🚀 Production-ready concerns: redis + memory hybrid caching, gzip compression, Application Insights, GovUK Frontend rebrand, and MassTransit transport setup.
- 🛠️ Admin tools: role-restricted admin pages let you view the current template metadata, clear caches/sessions, and publish a new template version directly through the UI (with validation and cache invalidation).

## Architecture Overview 🧱
- **Web (Razor Pages)**: UI, request pipeline, auth, cookies, session, and form rendering (`Pages/FormEngine`, `Pages/Applications`, feedback, notifications).
- **Application**: Interfaces for form orchestration, state, validation, uploads, and event mapping.
- **Domain**: Template and form primitives (`FormTemplate`, `TaskGroup`, `Task`, `Page`, `Field`, `ConditionalLogic`, `EventMapping`).
- **Infrastructure**: Implementations that call the External Applications API (`IApplicationsClient`, `ITemplatesClient`, `INotificationsClient`), template stores/parsers, MassTransit consumers, redis-backed caching, and file upload handling.
- **Tests**: Unit tests for infrastructure/web and Cypress end-to-end specs.

## Domain Model Relationships (Template Models)

The template engine is built around a hierarchy of domain models in `GovUK.Dfe.FlexForms.Domain.Models`. The `FormTemplate` is the root, containing task groups, tasks, pages, and fields, with conditional logic attached at the template level.

```mermaid
classDiagram
    direction TB

    class FormTemplate {
        +string TemplateId
        +string TemplateName
        +string Description
        +string? DefaultFieldRequirementPolicy
        +List~TaskGroup~ TaskGroups
        +List~ConditionalLogic~? ConditionalLogic
    }

    class TaskGroup {
        +string GroupId
        +string GroupName
        +int GroupOrder
        +string GroupStatus
        +List~Task~ Tasks
    }

    class Task {
        +string TaskId
        +string TaskName
        +string? Caption
        +int TaskOrder
        +string TaskStatusString
        +TaskStatus TaskStatus
        +List~Page~? Pages
        +bool? StartAtFirstPageWhenNotStarted
        +bool? VisibleInTaskList
        +TaskSummaryConfiguration? Summary
    }

    class TaskStatus {
        <<enum>>
        NotStarted = 0
        InProgress = 1
        Completed = 2
        CannotStartYet = 3
    }

    class TaskSummaryConfiguration {
        +string Mode
        +string? Title
        +string? Description
        +List~MultiCollectionFlowConfiguration~? Flows
        +List~DerivedCollectionFlowConfiguration~? DerivedFlows
    }

    class MultiCollectionFlowConfiguration {
        +string FlowId
        +string Title
        +string? Description
        +string FieldId
        +string AddButtonLabel
        +int? MinItems
        +int? MaxItems
        +string? ItemKind
        +string? ItemKindPlural
        +string? ItemTitleBinding
        +string TableType
        +List~FlowSummaryColumn~? SummaryColumns
        +List~Page~ Pages
    }

    class DerivedCollectionFlowConfiguration {
        +string FlowId
        +string Title
        +string? Description
        +string SourceFieldId
        +string SourceType
        +string FieldId
        +string ItemTitleBinding
        +int SectionOrder
        +string StatusField
        +List~Page~ Pages
    }

    class FlowSummaryColumn {
        +string Label
        +string Field
    }

    class DerivedCollectionItem {
        +string Id
        +string DisplayName
        +string Status
        +Dictionary PrefilledData
        +Dictionary? SourceData
    }

    class Page {
        +string PageId
        +string Slug
        +string Title
        +string Description
        +int PageOrder
        +bool ReturnToSummaryPage
        +string? SaveButtonLabel
        +List~Field~ Fields
    }

    class Field {
        +string FieldId
        +string Type
        +Label Label
        +string? Placeholder
        +string? Tooltip
        +bool? Required
        +int Order
        +string? Value
        +Visibility? Visibility
        +List~ValidationRule~? Validations
        +List~Option~? Options
        +ComplexField? ComplexField
    }

    class Label {
        +string Value
        +bool IsVisible
        +string? ValidationLabelValue
    }

    class Visibility {
        +bool Default
    }

    class ValidationRule {
        +string Type
        +object Rule
        +string Message
        +Condition? Condition
    }

    class Option {
        +string Value
        +string Label
    }

    class ComplexField {
        +string Id
    }

    class ComplexFieldConfiguration {
        +string Id
        +string ApiEndpoint
        +string ApiKey
        +string FieldType
        +bool AllowMultiple
        +int MinLength
        +string Placeholder
        +int MaxSelections
        +string Label
        +Dictionary AdditionalProperties
    }

    class ConditionalLogic {
        +string Id
        +string? Name
        +int Priority
        +bool Enabled
        +int Debounce
        +List~string~ ExecuteOn
        +ConditionGroup ConditionGroup
        +List~AffectedElement~ AffectedElements
    }

    class ConditionGroup {
        +string LogicalOperator
        +List~Condition~ Conditions
    }

    class Condition {
        +string TriggerField
        +string Operator
        +object Value
        +string DataType
        +string? LogicalOperator
        +List~Condition~? Conditions
    }

    class AffectedElement {
        +string ElementId
        +string ElementType
        +string Action
        +Dictionary? ActionConfig
    }

    class FormConditionalState {
        +Dictionary FieldVisibility
        +Dictionary PageVisibility
        +Dictionary FieldRequired
        +Dictionary FieldEnabled
        +Dictionary FieldValues
        +Dictionary AdditionalValidations
        +List~ConditionalLogicMessage~ Messages
        +HashSet SkippedPages
        +ConditionalLogicResult? EvaluationResult
        +DateTime CalculatedAt
    }

    class ConditionalLogicResult {
        +List~ConditionalLogicAction~ Actions
        +List~string~ Errors
        +bool IsSuccess
        +List~string~ EvaluatedRules
    }

    class ConditionalLogicAction {
        +AffectedElement Element
        +string RuleId
        +int Priority
        +DateTime CreatedAt
    }

    class ConditionalLogicMessage {
        +string Text
        +string Type
        +string? TargetElement
        +string? RuleId
    }

    FormTemplate "1" *-- "1..*" TaskGroup : TaskGroups
    FormTemplate "1" o-- "0..*" ConditionalLogic : ConditionalLogic

    TaskGroup "1" *-- "1..*" Task : Tasks

    Task "1" o-- "0..*" Page : Pages
    Task "1" --> "1" TaskStatus : TaskStatus
    Task "1" o-- "0..1" TaskSummaryConfiguration : Summary

    TaskSummaryConfiguration "1" o-- "0..*" MultiCollectionFlowConfiguration : Flows
    TaskSummaryConfiguration "1" o-- "0..*" DerivedCollectionFlowConfiguration : DerivedFlows

    MultiCollectionFlowConfiguration "1" *-- "1..*" Page : Pages
    MultiCollectionFlowConfiguration "1" o-- "0..*" FlowSummaryColumn : SummaryColumns

    DerivedCollectionFlowConfiguration "1" *-- "1..*" Page : Pages

    Page "1" *-- "1..*" Field : Fields

    Field "1" *-- "1" Label : Label
    Field "1" o-- "0..1" Visibility : Visibility
    Field "1" o-- "0..*" ValidationRule : Validations
    Field "1" o-- "0..*" Option : Options
    Field "1" o-- "0..1" ComplexField : ComplexField

    ComplexField ..> ComplexFieldConfiguration : resolved by Id

    ValidationRule "1" o-- "0..1" Condition : Condition

    ConditionalLogic "1" *-- "1" ConditionGroup : ConditionGroup
    ConditionalLogic "1" *-- "1..*" AffectedElement : AffectedElements

    ConditionGroup "1" *-- "1..*" Condition : Conditions
    Condition "1" o-- "0..*" Condition : Conditions (self-ref)

    FormConditionalState "1" o-- "0..1" ConditionalLogicResult : EvaluationResult
    FormConditionalState "1" o-- "0..*" ConditionalLogicMessage : Messages
    FormConditionalState "1" o-- "0..*" ValidationRule : AdditionalValidations

    ConditionalLogicResult "1" o-- "0..*" ConditionalLogicAction : Actions
    ConditionalLogicAction "1" *-- "1" AffectedElement : Element
```

## Application Response JSON Structure

When a user fills in a form, the engine saves responses as a flat JSON dictionary. Every entry has the shape `{ "value": "...", "completed": true|false }`. There are two categories of keys:

- **Field responses** keyed by `fieldId` (e.g. `"academiesSearch"`)
- **Task statuses** keyed by `"TaskStatus_{taskId}"` (e.g. `"TaskStatus_task-1"`)

```mermaid
graph TD
    subgraph ResponseJSON["Response JSON (flat dictionary)"]
        direction TB

        subgraph FieldEntries["Field Responses (keyed by fieldId)"]
            F1["academiesSearch<br/>{ value: '[{name,ukprn,urn}]', completed: true }"]
            F2["Data_academiesSearch<br/>{ value: '[]', completed: true }"]
            FN["...other field responses..."]
        end

        subgraph TaskEntries["Task Status Entries (keyed by TaskStatus_ + taskId)"]
            T1["TaskStatus_task-1<br/>{ value: 'Completed', completed: true }"]
            T2["TaskStatus_task-2<br/>{ value: 'Completed', completed: true }"]
            T3["TaskStatus_task-3<br/>{ value: 'Completed', completed: true }"]
            T4["TaskStatus_task-4<br/>{ value: 'Completed', completed: true }"]
            T5["TaskStatus_reason-and-benefits-academies<br/>{ value: 'Completed', completed: true }"]
            T6["TaskStatus_reason-and-benefits-trust<br/>{ value: 'Completed', completed: true }"]
            T7["TaskStatus_risks<br/>{ value: 'Completed', completed: true }"]
            T8["TaskStatus_high-quality-and-inclusive-education<br/>{ value: 'Completed', completed: true }"]
            T9["TaskStatus_school-improvement<br/>{ value: 'Completed', completed: true }"]
        end
    end

    subgraph TemplateModel["FormTemplate Model"]
        FT["FormTemplate"]
        TG["TaskGroup"]
        TK["Task (taskId)"]
        PG["Page"]
        FD["Field (fieldId)"]

        FT -->|taskGroups| TG
        TG -->|tasks| TK
        TK -->|pages| PG
        PG -->|fields| FD
    end

    F1 -.->|"key = Field.FieldId"| FD
    F2 -.->|"Data_ prefix stripped<br/>on normalize"| FD
    T1 -.->|"key = TaskStatus_ + Task.TaskId"| TK
    T5 -.->|"key = TaskStatus_ + Task.TaskId"| TK

    subgraph EntryShape["Each Entry Shape"]
        VS["{<br/>  value: string,<br/>  completed: bool<br/>}"]
    end
```

## Response Save & Load Data Flow

The sequence below shows how `ApplicationResponseService` and `ApplicationStateService` collaborate to persist and restore application responses via session and the External Applications API.

```mermaid
sequenceDiagram
    participant User as User (Browser)
    participant RF as RenderForm.OnPostAsync
    participant ARS as ApplicationResponseService
    participant Session as ISession (Redis-backed)
    participant API as External Applications API
    participant ASS as ApplicationStateService

    Note over User,API: === SAVE FLOW (Form Submit) ===

    User->>RF: POST form data (field values)
    RF->>RF: Collect Data dictionary<br/>from form fields (fieldId -> value)

    RF->>ARS: SaveApplicationResponseAsync(appId, Data, session)
    activate ARS

    ARS->>ARS: AccumulateFormData(newData, session)
    Note right of ARS: Merges new fields into session<br/>Normalizes Data_ prefix<br/>Removes duplicate keys<br/>Filters infected files (Redis blacklist)
    ARS->>Session: SetString("AccumulatedFormData", json)

    ARS->>Session: GetString("AccumulatedFormData")
    Session-->>ARS: allFormData dict

    ARS->>Session: Get keys starting with<br/>TaskStatus_{appId}_*
    Session-->>ARS: taskStatusData dict<br/>(taskId -> Completed/InProgress/...)

    ARS->>ARS: TransformToResponseJson(allFormData, taskStatusData)
    Note right of ARS: For each field:<br/>  fieldId -> { value, completed }<br/>  completed = !IsNullOrWhiteSpace(value)<br/><br/>For each task status:<br/>  TaskStatus_{taskId} -> { value, completed: true }

    ARS->>ARS: Base64 encode JSON
    ARS->>API: AddApplicationResponseAsync(appId, base64Body)

    ARS->>ARS: EnsureApplicationStatusIsInProgress
    deactivate ARS

    Note over User,API: === SAVE TASK STATUS ===

    RF->>ASS: SaveTaskStatusAsync(appId, taskId, status, session)
    activate ASS
    ASS->>ARS: SaveTaskStatusToSession(appId, taskId, status)
    ARS->>Session: SetString("TaskStatus_{appId}_{taskId}", status)
    ASS->>ARS: SaveApplicationResponseAsync(appId, {}, session)
    Note right of ASS: Triggers full response rebuild<br/>with updated task status
    deactivate ASS

    Note over User,API: === LOAD FLOW (Resume Application) ===

    User->>RF: GET /applications/{ref}/{taskId}/{pageId}
    RF->>ASS: EnsureApplicationIdAsync(ref, session)
    activate ASS
    ASS->>API: GetApplicationByReferenceAsync(ref)
    API-->>ASS: ApplicationDto (includes LatestResponse.ResponseBody)

    ASS->>ASS: LoadResponseDataIntoSessionAsync(app, session)
    Note right of ASS: Parse response JSON<br/><br/>For TaskStatus_* keys:<br/>  Extract taskId, restore to session<br/>  as TaskStatus_{appId}_{taskId}<br/><br/>For field keys:<br/>  Extract .value from {value, completed}<br/>  Build formDataDict

    ASS->>ARS: StoreFormDataInSession(formDataDict)
    ARS->>Session: SetString("AccumulatedFormData", json)
    ASS->>ARS: SetCurrentAccumulatedApplicationId(appId)
    deactivate ASS
```

## System Flow (happy path) 🔄
1. **Sign in** via DfE Sign-in (or test/internal auth in non-prod). Permissions are cached and refreshed via `TokenRefresh` settings.
2. **Dashboard** shows the user’s applications for the configured template; “Create” calls `CreateApplicationAsync`, stores IDs in session, and clears cached form data.
3. **Contributors** page lets the lead applicant manage collaborators before entering the form.
4. **Form engine** loads the current template (cached via `FormTemplateProvider` + `ApiTemplateStore`), restores response data from the API/session, and renders pages with conditional logic, collection flows, and complex fields (autocomplete, upload).
5. **File uploads** are sent to the API; scan results arrive on Azure Service Bus and `ScanResultConsumer` removes infected files, clears redis cache, and raises a user notification.
6. **Validation and navigation** are handled server-side; task completion is tracked per-task and persisted back through `IApplicationResponseService`.
7. **Submit** posts final responses to the API and publishes `TransferApplicationSubmittedEvent` to the Service Bus using the configured event mapping for the transfer template.

Example event publication in the form engine:

```3554:3592:src/GovUK.Dfe.FlexForms.Web/Pages/FormEngine/RenderForm.cshtml.cs
        /// Publishes the TransferApplicationSubmittedEvent to the service bus
        /// Uses the event data mapper to extract and transform form data according to the configured mapping
        private async Task PublishApplicationSubmittedEventAsync(ApplicationDto application)
        {
            var eventData = await _eventDataMapper.MapToEventAsync<TransferApplicationSubmittedEvent>(
                FormData,
                Template,
                "transfer-application-submitted-v1",
                application.ApplicationId,
                application.ApplicationReference);
            await publishEndpoint.PublishAsync(eventData, messageProperties, CancellationToken.None);
        }
```

## Key configuration ⚙️
All settings are standard ASP.NET Core configuration keys (appsettings or environment variables). Important ones:

| Key | Purpose | Dev value / notes |
| --- | --- | --- |
| `DfESignIn:Authority` / `ClientId` / `ClientSecret` / `RedirectUri` | OIDC login | Dev authority `https://test-oidc.signin.education.gov.uk`; set secrets locally. |
| `ExternalApplicationsApiClient:BaseUrl` | Backend API endpoint | `https://api.dev.apply-transfer-academy.service.gov.uk` (see `appsettings.Development.json`). |
| `ExternalApplicationsApiClient:ClientId` / `ClientSecret` / `Authority` / `Scope` | API auth (Azure AD) | ClientId preset; supply `ClientSecret` via user-secrets/env. |
| `Template:Id` | Template to render | Default transfer template `9A4E9C58-9135-468C-B154-7B966F7ACFB7`. |
| `FormEngine:ComplexFields` | External search endpoints | Dev APIs for trusts and establishments are prefilled in `appsettings.Development.json`. |
| `MassTransit:Transport` / `AzureServiceBus:ConnectionString` | Service Bus for events & AV scan results | Provide connection string to receive scan results and publish submissions. |
| `ConnectionStrings:Redis` | Redis for hybrid caching and sessions | Default `localhost:6379`. |
| `ApplicationInsights:ConnectionString` | Telemetry | Optional locally; required in cloud. |
| `TokenRefresh:*` | Session/token refresh windows | Defaults provided in `appsettings.json`. |
| `InternalServiceAuth:*` | Service-to-service auth | Used for internal APIs and virus-scan cleanup. |

## Running locally 🖥️
Prerequisites: .NET 8 SDK, Redis (local or container), Node/npm if running Cypress, and access to the dev External Applications API + Azure AD app registration.

1) Clone and restore  
```bash
dotnet restore GovUK.Dfe.FlexForms.Web.sln
```

2) Configure secrets (examples)  
```bash
# Auth & token refresh
dotnet user-secrets set "DfESignIn:ClientSecret" "<oidc-client-secret>" --project src/GovUK.Dfe.FlexForms.Web
dotnet user-secrets set "TokenRefresh:ClientSecret" "<oidc-client-secret>" --project src/GovUK.Dfe.FlexForms.Web

# External Applications API auth
dotnet user-secrets set "ExternalApplicationsApiClient:ClientSecret" "<api-client-secret>" --project src/GovUK.Dfe.FlexForms.Web

# Messaging / telemetry / cache
dotnet user-secrets set "MassTransit:AzureServiceBus:ConnectionString" "<sb-connection>" --project src/GovUK.Dfe.FlexForms.Web
dotnet user-secrets set "ApplicationInsights:ConnectionString" "<ai-connection>" --project src/GovUK.Dfe.FlexForms.Web
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" --project src/GovUK.Dfe.FlexForms.Web

# Internal service-to-service auth
dotnet user-secrets set "InternalServiceAuth:SecretKey" "<internal-signing-key>" --project src/GovUK.Dfe.FlexForms.Web
dotnet user-secrets set "InternalServiceAuth:Services:0:ApiKey" "<internal-api-key>" --project src/GovUK.Dfe.FlexForms.Web

# Optional: secured downstream APIs for complex fields (if required) 🔒
# Matching structure:
# {
#   "Id": "TrustComplexField",
#   "ApiEndpoint": "https://api.dev.academies.education.gov.uk/trusts?page=1&count=10&groupname={0}&ukprn={0}&companieshousenumber={0}&status=Open",
#   "ApiKey": "<trusts-api-key>"
# },
# {
#   "Id": "EstablishmentComplexField",
#   "ApiEndpoint": "https://api.dev.academies.education.gov.uk/v4/establishments?page=1&count=10&name={0}&urn={0}&ukprn={0}&matchAny=true&excludeClosed=true",
#   "ApiKey": "<establishments-api-key>"
# }
dotnet user-secrets set "FormEngine:ComplexFields:0:ApiKey" "<trusts-api-key>" --project src/GovUK.Dfe.FlexForms.Web
dotnet user-secrets set "FormEngine:ComplexFields:1:ApiKey" "<establishments-api-key>" --project src/GovUK.Dfe.FlexForms.Web
```

3) Use the dev API configuration  
`ASPNETCORE_ENVIRONMENT=Development` uses `appsettings.Development.json`, which already points to `https://api.dev.apply-transfer-academy.service.gov.uk` and the dev academies search endpoints.

4) Run the web app  
```bash
dotnet run --project src/GovUK.Dfe.FlexForms.Web/GovUK.Dfe.FlexForms.Web.csproj
```
Browse to `https://localhost:5001` (or the HTTPS port shown in the console) and sign in with a dev DfE Sign-in account.

5) Optional: receive virus-scan events  
Ensure the Service Bus connection string is set so `ScanResultConsumer` can process `ScanResultEvent` messages and clean infected uploads.

## Tests ✅
- Unit tests: `dotnet test GovUK.Dfe.FlexForms.Web.sln`
- Cypress (E2E): from `Tests/GovUK.Dfe.FlexForms.CypressTests`, install deps then run `npm test` or `npx cypress run`.

## Example workflow 📋
1. Create a new application from the dashboard (uses the configured template ID).
2. Add contributors and proceed to the form.
3. Complete tasks/pages; upload supporting documents (they will be virus-scanned).
4. Submit; the app persists responses to the External Applications API and publishes `TransferApplicationSubmittedEvent` to Service Bus for downstream processing.

## Admin pages (template management) 🛠️
- Access: available to users in role `Admin`; entry point at `/Admin/Admin` with a link to `/Admin/TemplateManager`.
- Admin dashboard: shows the current template ID/name/description/version, task group count, cache key status, and tokens; provides a “clear all” to wipe session and template cache.
- Template Manager:
  - Displays the current template JSON and latest version (fetched from the API; cache is cleared before loading).
  - “Add template version” flow validates the provided JSON against the form template schema, base64-encodes it, and calls `CreateTemplateVersionAsync`.
  - Auto-suggests the next patch version, and after creating a version, clears and verifies cache so the new schema is served immediately.
  - Includes “clear all” to drop session and cache and return to dashboard if the template ID is lost.

## Notes 🧠
- Clean Architecture boundaries are enforced: web depends on application interfaces, implementations live in Infrastructure, and templates are domain-driven JSON schemas.
- If you change template IDs or event mappings, update `Template:Id` and the mapping under `EventMappings/<template>/`.

