# Code map (flexforms-web)

**Verified** against source on 2026-11-17. Where to find things. Platform-wide context: [`ecosystem.md`](ecosystem.md).

## Projects

| Project | Path | Contents |
|---------|------|----------|
| Domain | `src/GovUK.Dfe.FlexForms.Domain` | No package references. `Models/` (template JSON model: `FormTemplate`, `TaskGroup`, `Task`, `Page`, `Field`, `ComplexField`, conditional logic, `DashboardConfiguration`, `TemplateDisplayCopy`, `EventMapping/`), `FormEngine/` (`FormRouteParser`, `FormStepPolicy`, `CheckboxValueNormalizer`), `Caching/FormSessionKeys.cs`, `Templates/` |
| Application | `src/GovUK.Dfe.FlexForms.Application` | Use-case services (ports + outcomes): `FormEngine/` (prepare GET, save page, complete task, submit, upload/delete/download file, remove collection item, draft preview, visibility, upload limits), `Admin/` (one `*AdminService` + `*WorkState` + `*Messages` per admin page), `Dashboard/`, `Applications/` (submitted page), `Contributors/` (`ContributorInviteDefaultAccess` checkbox defaults, `ContributorAccessLabels` for list cards, `TemplateUploadFieldDetector`), `Interfaces/` (ports such as `IFormTemplateProvider`, `IFormStateManager`, `IConditionalLogicEngine`, `ITwoLevelCache`, `IInfectedFileStore`), `Validation/`, `DisplayCopy/`, `Exceptions/` (`StaleFormSessionException`) |
| Infrastructure | `src/GovUK.Dfe.FlexForms.Infrastructure` | Adapters: `Services/` (`ApplicationResponseService`, `ApplicationStateService`, `FormStateManager`, `FormNavigationService`, `ConditionalLogicEngine` + `Orchestrator`, `DerivedCollectionFlowService`, `FormValidationOrchestrator`, complex-field renderers, `ContributorService`, `TemplateManagementService`), `Stores/` (`ApiTemplateStore`, `HttpFormSessionStore`, `RedisInfectedFileStore`, `JsonFileTemplateStore`), `Providers/` (`FormTemplateProvider`, `SchemaEventDefinitionProvider`), `Caching/` (`TwoLevelCache`, `RedisSharedCacheStore`, `ReplicaBroadcastService`), `Consumers/ScanResultConsumer.cs`, `Messaging/`, `Parsers/JsonFormTemplateParser.cs`, `DependencyInjection.cs` (`AddInfrastructureDependencyGroup`) |
| Web | `src/GovUK.Dfe.FlexForms.Web` | `Program.cs` (composition root, middleware order), `Pages/` (thin PageModels), `Controllers/` (JSON endpoints), `Middleware/`, `Security/` (scheme selection, OIDC/Entra overlays, ticket store, permissions cache, Data Protection, admin policies), `Authentication/` (Test + internal service handlers), `Services/` (Api-facing helpers, `Platform/` bootstrap, `Tenant/` resolver + caches + invalidator), `Tenancy/` (`ITenantRequestContext`), `Telemetry/`, `Views/Shared/Fields/` (field partials), `wwwroot/js/` (`notifications.js`, `template-studio/`), `Features/` (FeatureManagement from tenant config) |
| Tests | `src/Tests/*` | Domain, Application, Infrastructure.UnitTests, Web.UnitTests (`Architecture/CleanArchitectureGuardTests.cs`), Playwright (NUnit). `src/Test` and Cypress are outside the solution |

## Pages

| Area | Pages | Use-case service |
|------|-------|-------------------|
| Form engine | `FormEngine/RenderForm` (dispatcher; `RenderForm.Support.cs`), `FormEngine/UploadFile`, `FormEngine/RemoveFieldItem` | `PrepareFormEngineGetService`, `SaveFormPageService`, `CompleteFormTaskService`, `SubmitFormApplicationService`, `UploadFormFileService`, `DeleteFormFileService`, `DownloadFormFileService`, `RemoveCollectionItemService` |
| Applicant | `Templates/Index` (choose a form), `Applications/Index`, `Applications/Dashboard`, `Applications/ApplicationSubmitted`, `Applications/Contributors`, `Applications/Contributors-Invite` (permission checkboxes), `Confirmation/Index`, `Notifications/Index` | `DashboardApplicationsService`, `PrepareApplicationSubmittedPageService`, `ContributorService` |
| Admin | `Admin/Admin` (hub), `Applications`, `ContributorManagement`, `CreateTemplate`, `TemplateManager`, `TemplateStudio`, `TemplateStudioPreview`, `CustomStatusLabelOverrides`, `UserManager`, `UserManagerAdd`, `UserManagerEdit`, `UserManagerPermissions`, `UserManagerAuthenticatorCode`, `RoleManager`, `RoleManagerPermissions`, `TenantSettings`, `OrganisationSettings`, `EventMappings`, `NewTenant`, `PlatformTenants`, `ReportingExport` | matching `Application/Admin/*AdminService` |
| Auth | `TestLogin`, `TestLogout`, `TestAuthenticator`, `Logout` | `TestAuthenticationService` |
| Other | `Feedback/*`, `Error/*`, `Shared/Cookies`, `Diagnostics`, `TestError` | |

Authorisation conventions (`Security/PageAccessConventions.cs`, applied by `AddRazorPages` in `Program.cs`): every page needs `OpenIdConnectPolicy`; anonymous pages are Logout, Feedback, Cookies, Error, TestLogin and TestLogout (both 404 unless Test Authentication is on) and, outside production, TestError. Every page's and controller action's effective rule is pinned in `ExpectedPageSecurity.json` (see `security.md`). Admin policies from `Security/AdminAccessHelper`: `CanAccessAdminArea`, `CanManageTemplates`, `CanManageUsers`, `CanManageTenantSettings`, `CanManagePlatformTenants`, `CanManageEventMappings`, `CanReadAnyApplication`. UI policies are UX only; the API decides.

## Controllers

`SessionController`, `FrontHubTicketController` (`/internal/hub-ticket` → API hub ticket), `NotificationsController` (`/notifications/unread` etc. proxy), `FileValidationMessageController` (`/file-validation/message/render`, Markdown → safe HTML), `CookiesController`.

## Middleware (order in `Program.cs`, do not reorder casually)

ForwardedHeaders → CorrelationId → `UsePlatformTenantConfiguration` (`TenantConfigurationMiddleware`) → exception handler/HSTS → security headers → HTTPS redirect (not health) → compression → static files → routing → cookie policy → request timing → **session** → status code pages → authentication → `TenantSessionBindingMiddleware` → reporting session binding → `UseTokenManagementMiddleware` (Api.Client `TokenManagementMiddleware`; Web's own `Middleware/TokenExpiryMiddleware.cs` is not registered) → `ActivityBasedTokenRefreshMiddleware` (idle 30 min, absolute 8 h, refresh at 30 min remaining) → `PermissionsCacheMiddleware` → authorization → `TemplateSelectionMiddleware` → `RequestTelemetryEnrichmentMiddleware` → GOV.UK Frontend → endpoints. Health `/health`, `/healthz`, `/liveness` are `.ShortCircuit()`.

## Error pages

**Verified** (2026-11). All are anonymous rules in `Security/PageAccessConventions.cs`. Reuse these before adding a page.

| Page | Use for | TempData it reads |
|------|---------|-------------------|
| `/Error/Forbidden` | Access denied (403, and 401 from the filter) | `AccessDeniedReason` (shown instead of `ApplicationAccessMessages.NoAccess`), `ApiErrorId` (error reference). Hides the dashboard link when `ApiErrorId` is the no-membership ID |
| `/Error/NotFound` | 404, `ApplicationAccessException`, and 403 on GET application pages (to avoid revealing that the application exists) | none |
| `/Error/General` | 429 and other API errors with a message | `ErrorMessage`, `ApiErrorId` |
| `/Error/ServerError` | 5xx and unhandled exceptions | `ApiErrorId` |

TempData is session-backed (`AddSessionStateTempDataProvider`), so it survives a cookie sign-out. Middleware has no `PageModel`, so it writes TempData through `ITempDataDictionaryFactory` and calls `Save()` (see `PermissionsCacheMiddleware`). Pattern for "sign out and say why": `TenantSessionBindingMiddleware` and the no-membership path in `PermissionsCacheMiddleware` (cookie sign-out only, then redirect). User-facing copy lives in `Constants/ApplicationAccessMessages.cs`.

## API usage

Typed clients come from `GovUK.Dfe.FlexForms.Api.Client` (`AddExternalApplicationsApiClients`): generated interfaces `IApplicationsClient`, `ITemplatesClient`, `IUsersClient`, `IRolesClient`, `INotificationsClient`, `ITenantAdminClient`, `ITenantConfigClient`, `IHostConfigClient`, `ITokensClient`, `IHubAuthClient`, `IUserFeedbackClient`, `IReportingExportClient`, `IDiagnosticsClient`, `IIntegrationsClient`, `IInternalPrismClient` (Prism only), plus Web's own `Services/ApplicationOwnerClient`. Settings per request from `Tenancy/TenantApiClientSettingsProvider`. Platform bootstrap calls use `Services/Platform/PlatformConfigurationApiClient` with an app-only token (`PlatformAccessTokenProvider`).

## Messaging

MassTransit (CoreLibs `AddDfEMassTransit`) in `Program.cs`: subscription `{MassTransit:SubscriptionPrefix}-scan-result` (default `extweb-scan-result`) on `file-scanner-results`, consumer `Infrastructure/Consumers/ScanResultConsumer.cs`, consume filter `ScanResultTenantContextFilter<>` sets the tenant from message headers. Web publishes nothing.

## Configuration

`appsettings.json` + `appsettings.bootstrap.json` (placeholders), then host-config from the API at startup, then per-tenant config (Target `Web`) per request. Notable host keys: `PlatformBootstrap:*` (`Enabled`, `TenantConfigurationCacheMinutes`), `Platform` (Entra for bootstrap), `DataProtection` (blob `web-keys.xml`), `CacheSettings` (memory / Redis durations keyed by method name), `MassTransit`, `Telemetry:ReportingUserIdKey`, `ApplicationInsights`. Tenant keys Web reads: `DfESignIn`, `EntraSso`, `TestAuthentication`, `TokenRefresh`, `ExternalApplicationsApiClient`, `FormEngine` (`ComplexFields`), `FeatureManagement` (`TemplateStudio`), `Layout`, `ApplicationTerminology`, `NotificationBanner`, `FrontendSettings`, `Template`, `ApplicationTemplates`.
