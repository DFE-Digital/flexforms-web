using GovUK.Dfe.FlexForms.Application.Exceptions;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Notifications;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Extensions;
using GovUK.Dfe.FlexForms.Web.Interfaces;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.Pages.FormEngine
{
    [ExcludeFromCodeCoverage]
    public class RenderFormModel(
        IFieldRendererService renderer,
        IApplicationResponseService applicationResponseService,
        IFieldFormattingService fieldFormattingService,
        ITemplateManagementService templateManagementService,
        IApplicationStateService applicationStateService,
        IFormStateManager formStateManager,
        IFormNavigationService formNavigationService,
        IFormDataManager formDataManager,
        IFormValidationOrchestrator formValidationOrchestrator,
        IFormConfigurationService formConfigurationService,
        IAutocompleteService autocompleteService,
        IConditionalLogicOrchestrator conditionalLogicOrchestrator,
        INotificationsClient notificationsClient,
        IFormErrorStore formErrorStore,
        IFormEnginePresentationComposer formEnginePresentationComposer,
        ICompleteFormTask completeFormTask,
        ISubmitFormApplication submitFormApplication,
        IPrepareFormEngineGet prepareFormEngineGet,
        ISaveFormPage saveFormPage,
        IRemoveCollectionItem removeCollectionItem,
        IUploadFormFile uploadFormFile,
        IDeleteFormFile deleteFormFile,
        IDownloadFormFile downloadFormFile,
        ILogger<RenderFormModel> logger,
        IRequestAppConfiguration requestConfiguration)
        : BaseFormEngineModel(renderer, applicationResponseService, fieldFormattingService, templateManagementService,
            applicationStateService, formStateManager, formNavigationService, formDataManager, formValidationOrchestrator, formConfigurationService, logger)
    {
        private readonly IConditionalLogicOrchestrator _conditionalLogicOrchestrator = conditionalLogicOrchestrator;
        private readonly INotificationsClient _notificationsClient = notificationsClient;
        private readonly IFormErrorStore _formErrorStore = formErrorStore;
        private readonly IFormEnginePresentationComposer _formEnginePresentationComposer = formEnginePresentationComposer;
        private readonly ICompleteFormTask _completeFormTask = completeFormTask;
        private readonly ISubmitFormApplication _submitFormApplication = submitFormApplication;
        private readonly IPrepareFormEngineGet _prepareFormEngineGet = prepareFormEngineGet;
        private readonly ISaveFormPage _saveFormPage = saveFormPage;
        private readonly IRemoveCollectionItem _removeCollectionItem = removeCollectionItem;
        private readonly IUploadFormFile _uploadFormFile = uploadFormFile;
        private readonly IDeleteFormFile _deleteFormFile = deleteFormFile;
        private readonly IDownloadFormFile _downloadFormFile = downloadFormFile;
        private readonly IRequestAppConfiguration _requestConfiguration = requestConfiguration;
        private FormEngineVisibilityEvaluator? _visibility;
        private string ApplicationContext =>
            _requestConfiguration["ApplicationName"]
            ?? _requestConfiguration["TenantName"]
            ?? "platform";

        [BindProperty(SupportsGet = false)] public Dictionary<string, object> Data { get; set; } = new();

        public new string BackLinkUrl => GetBackLinkUrl();

        [BindProperty] public bool IsTaskCompleted { get; set; }

        [BindProperty] public new string? FlowId { get; set; }
        [BindProperty] public new string? InstanceId { get; set; }
        [BindProperty] public string? FlowPageId { get; set; }

        [BindProperty] public string? DerivedFlowId { get; set; }
        [BindProperty] public string? DerivedItemId { get; set; }
        [BindProperty] public string? DerivedPageId { get; set; }

        private bool IsCollectionFlow => !string.IsNullOrEmpty(FlowId) && !string.IsNullOrEmpty(InstanceId);

        [TempData] public string? SuccessMessage { get; set; }

        [TempData] public string? ErrorMessage { get; set; }

        public IReadOnlyList<UploadDto> Files { get; set; } = new List<UploadDto>();

        public bool FileValidationBlocksSubmit { get; set; }

        public IReadOnlyList<FileValidationBlockDto> FileValidationBlockingFiles { get; set; } = [];

        public ApplicationPreviewViewModel? Preview { get; private set; }

        public IReadOnlyList<CollectionFlowSectionViewModel> CollectionFlows { get; private set; } = [];

        public FormConditionalState? ConditionalState { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                await CommonFormEngineInitializationAsync();
            }
            catch (ApplicationAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CommonFormEngineInitializationAsync for ReferenceNumber: {ReferenceNumber}", ReferenceNumber);
                throw;
            }

            var state = CaptureWorkState();
            var isPreview = Request.Query.ContainsKey("preview");
            var isBackNav = Request.Query.ContainsKey("nav")
                && string.Equals(Request.Query["nav"], "back", StringComparison.OrdinalIgnoreCase);
            var outcome = await _prepareFormEngineGet.ExecuteAsync(state, isPreview, isBackNav, IsApplicationEditable());
            ApplyWorkState(state);

            if (outcome.Kind is FormEngineOutcomeKind.Redirect or FormEngineOutcomeKind.RedirectToPage)
                return MapOutcome(outcome);

            var result = MapOutcome(outcome);
            RestoreFormErrors();
            ViewData["ValidationErrors"] = ModelState.Where(m => m.Value != null && m.Value.Errors.Any())
                .ToDictionary(m => m.Key, m => m.Value!.Errors.Select(e => e.ErrorMessage).ToList());
            return result;
        }

        public override void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
            BuildPresentationViewModels();
            base.OnPageHandlerExecuted(context);
        }

        public static string BuildHistoryScope(string referenceNumber, string taskId, string currentPageId) =>
            FormRouteParser.HistoryScope(referenceNumber, taskId, currentPageId);

        private FormEngineWorkState CaptureWorkState() =>
            new()
            {
                ReferenceNumber = ReferenceNumber,
                TaskId = TaskId,
                CurrentPageId = CurrentPageId,
                ApplicationId = ApplicationId,
                ApplicationStatus = ApplicationStatus,
                Template = Template,
                FormData = FormData,
                Data = Data,
                CurrentFormState = CurrentFormState,
                CurrentGroup = CurrentGroup,
                CurrentTask = CurrentTask,
                CurrentPage = CurrentPage,
                FlowId = FlowId,
                InstanceId = InstanceId,
                FlowPageId = FlowPageId,
                DerivedFlowId = DerivedFlowId,
                DerivedItemId = DerivedItemId,
                DerivedPageId = DerivedPageId,
                ConditionalState = ConditionalState,
                IsEditable = IsApplicationEditable(),
                IsTaskCompleted = IsTaskCompleted
            };

        private void ApplyWorkState(FormEngineWorkState state)
        {
            ReferenceNumber = state.ReferenceNumber;
            TaskId = state.TaskId;
            CurrentPageId = state.CurrentPageId;
            ApplicationId = state.ApplicationId;
            ApplicationStatus = state.ApplicationStatus;
            Template = state.Template;
            FormData = state.FormData;
            Data = state.Data;
            CurrentFormState = state.CurrentFormState;
            CurrentGroup = state.CurrentGroup;
            CurrentTask = state.CurrentTask;
            CurrentPage = state.CurrentPage;
            FlowId = state.FlowId;
            InstanceId = state.InstanceId;
            FlowPageId = state.FlowPageId;
            DerivedFlowId = state.DerivedFlowId;
            DerivedItemId = state.DerivedItemId;
            DerivedPageId = state.DerivedPageId;
            ConditionalState = state.ConditionalState;
            IsTaskCompleted = state.IsTaskCompleted;
            _visibility = null;
        }

        private IActionResult MapOutcome(FormEngineOutcome outcome)
        {
            foreach (var key in outcome.ModelStateKeysToRemove)
                ModelState.Remove(key);

            if (outcome.ClearModelState)
                ModelState.Clear();

            if (outcome.Errors.Count > 0)
                new FormValidationResult(outcome.Errors).ApplyTo(ModelState);

            if (outcome.FormState is { } formState)
                CurrentFormState = formState;

            if (outcome.IsTaskCompleted is { } completed)
                IsTaskCompleted = completed;

            if (outcome.SuccessMessage != null)
                SuccessMessage = outcome.SuccessMessage;

            if (outcome.ErrorMessage != null)
                ErrorMessage = outcome.ErrorMessage;

            if (outcome.Files != null)
                Files = outcome.Files;

            FileValidationBlocksSubmit = outcome.FileValidationBlocksSubmit;
            if (outcome.BlockingFiles.Count > 0)
                FileValidationBlockingFiles = outcome.BlockingFiles;

            if (outcome.ConditionalState != null)
                ConditionalState = outcome.ConditionalState;

            if (outcome.ReloadFormData)
                LoadFormDataFromSession();

            foreach (var key in outcome.ErrorStoreKeysToClear)
                _formErrorStore.Clear(key);

            if (outcome.PersistErrors && !string.IsNullOrEmpty(outcome.ErrorContextKey))
                _formErrorStore.Save(outcome.ErrorContextKey, ModelState);

            return outcome.Kind switch
            {
                FormEngineOutcomeKind.StayOnPage => Page(),
                FormEngineOutcomeKind.Redirect => Redirect(outcome.RedirectUrl!),
                FormEngineOutcomeKind.RedirectToPage => RedirectToPage(outcome.PageName, outcome.RouteValues),
                FormEngineOutcomeKind.NotFound => NotFound(),
                FormEngineOutcomeKind.BadRequest => BadRequest(outcome.ErrorMessage),
                FormEngineOutcomeKind.FileDownload => File(outcome.FileStream!, outcome.FileContentType!, outcome.FileDownloadName),
                _ => Page()
            };
        }

        private void BuildPresentationViewModels()
        {
            if (Template == null)
                return;

            var presentationContext = CreatePresentationContext();

            if (CurrentFormState == FormState.ApplicationPreview)
                Preview = _formEnginePresentationComposer.BuildPreview(presentationContext);

            if (CurrentFormState == FormState.TaskSummary
                && CurrentTask != null
                && FormStepPolicy.IsCollectionFlowSummary(CurrentTask))
            {
                CollectionFlows = _formEnginePresentationComposer.BuildCollectionFlows(presentationContext, CurrentTask);
            }
        }

        private FormEnginePresentationContext CreatePresentationContext()
        {
            var submitDisabled = _requestConfiguration.GetSection("Layout:SubmitAppDisabled").Exists();
            return new FormEnginePresentationContext
            {
                Template = Template,
                FormData = FormData,
                ReferenceNumber = ReferenceNumber,
                TaskId = TaskId,
                ApplicationId = ApplicationId,
                InfectedFilterApplicationId = ApplicationId?.ToString()
                    ?? HttpContext.Session.GetString(FormSessionKeys.ApplicationId),
                IsEditable = IsApplicationEditable(),
                IsLeadApplicant = IsCurrentUserLeadApplicant(),
                SubmitDisabledByConfig = submitDisabled,
                SubmitDisabledBannerText = submitDisabled
                    ? _requestConfiguration["Layout:SubmitAppDisabled:BannerText"]
                    : null,
                SubmitDisabledHelpText = submitDisabled
                    ? _requestConfiguration["Layout:SubmitAppDisabled:HelpText"]
                    : null,
                FileValidationBlocksSubmit = FileValidationBlocksSubmit,
                BlockingFiles = FileValidationBlockingFiles,
                IncludePreviewQuery = Request.Query.ContainsKey("preview"),
                EnsureItemFieldVisibility = EnsureItemFieldVisibility,
                IsFieldHiddenForItem = IsFieldHiddenForItem,
                IsFieldHidden = IsFieldHidden
            };
        }

        private new bool IsCurrentUserLeadApplicant()
        {
            var applicationId = HttpContext.Session.GetString(FormSessionKeys.ApplicationId);
            var leadApplicantEmail = HttpContext.Session.GetString($"ApplicationLeadApplicantEmail_{applicationId}");
            var currentUserEmail = User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("email")?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.Identity?.Name;

            return string.Equals(
                currentUserEmail?.Trim(),
                leadApplicantEmail?.Trim(),
                StringComparison.InvariantCultureIgnoreCase);
        }

        public async Task<IActionResult> OnPostTaskSummaryAsync()
        {
            await CommonFormEngineInitializationAsync();

            if (!string.IsNullOrEmpty(TaskId))
            {
                var (group, task) = InitializeCurrentTask(TaskId);
                CurrentGroup = group;
                CurrentTask = task;
                CurrentPage = null;
            }

            var state = CaptureWorkState();
            var outcome = await _completeFormTask.ExecuteAsync(state);
            ApplyWorkState(state);
            return MapOutcome(outcome);
        }

        public async Task<IActionResult> OnPostSubmitApplicationAsync()
        {
            ModelState.Remove(nameof(TaskId));
            ModelState.Remove(nameof(CurrentPageId));
            ModelState.Remove("TaskId");
            ModelState.Remove("CurrentPageId");
            ModelState.Remove("pageId");
            ModelState.Remove("taskId");

            await CommonFormEngineInitializationAsync();

            var state = CaptureWorkState();
            state.IsEditable = IsApplicationEditable();
            var outcome = await _submitFormApplication.ExecuteAsync(state);
            ApplyWorkState(state);
            return MapOutcome(outcome);
        }

        public async Task<IActionResult> OnPostPageAsync()
        {
            _logger.LogInformation("POST: OnPostPageAsync called - ReferenceNumber='{ReferenceNumber}', TaskId='{TaskId}', CurrentPageId='{CurrentPageId}'",
                ReferenceNumber, TaskId, CurrentPageId);
            _logger.LogInformation("POST: Request URL: {Url}", $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}");
            _logger.LogInformation("POST: Form data keys: {Keys}", string.Join(", ", Request.Form.Keys));

            ModelState.Remove(nameof(CurrentPageId));
            ModelState.Remove("pageId");

            if (Request.Query.ContainsKey("confirmed") && Request.Query["confirmed"] == "true")
            {
                var confirmedDataJson = TempData["ConfirmedFormData"]?.ToString();
                var confirmedHandler = TempData["ConfirmedHandler"]?.ToString();

                if (!string.IsNullOrEmpty(confirmedDataJson))
                {
                    try
                    {
                        var confirmedData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(confirmedDataJson);
                        if (confirmedData != null)
                        {
                            foreach (var kvp in confirmedData)
                                Data[kvp.Key] = kvp.Value;
                            _logger.LogInformation("Restored {Count} confirmed form fields for handler {Handler}",
                                confirmedData.Count, confirmedHandler);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize confirmed form data");
                    }
                }
            }

            await CommonFormEngineInitializationAsync();

            if (!string.IsNullOrEmpty(CurrentPageId))
                CurrentPageId = System.Web.HttpUtility.UrlDecode(CurrentPageId);

            var state = CaptureWorkState();
            var outcome = await _saveFormPage.ExecuteAsync(
                state,
                Request.Form.ToPostedFields(),
                Request.Form["IsTaskCompleted"].ToString());
            ApplyWorkState(state);
            return MapOutcome(outcome);
        }

        public async Task<IActionResult> OnGetAutocompleteAsync(string endpoint, string query)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return new JsonResult(new List<object>());

            try
            {
                var results = await autocompleteService.SearchAsync(endpoint, query);
                _logger.LogInformation("Autocomplete search returned {Count} results", results.Count);
                return new JsonResult(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in autocomplete search endpoint: {Endpoint}, query: {Query}", endpoint, query);
                return new JsonResult(new List<object>());
            }
        }

        public async Task<IActionResult> OnPostRemoveCollectionItemAsync(string fieldId, string itemId, string? flowId = null)
        {
            await CommonFormEngineInitializationAsync();
            ModelState.Clear();

            var state = CaptureWorkState();
            var confirmed = Request.Query.ContainsKey("confirmed") && Request.Query["confirmed"] == "true";
            var outcome = await _removeCollectionItem.ExecuteAsync(state, fieldId, itemId, flowId, confirmed);
            ApplyWorkState(state);
            return MapOutcome(outcome);
        }

        public async Task<IActionResult> OnGetComplexFieldAsync(string complexFieldId, string query)
        {
            _logger.LogInformation("Complex field search called with complexFieldId: {ComplexFieldId}, query: {Query}", complexFieldId, query);

            if (string.IsNullOrWhiteSpace(complexFieldId))
            {
                _logger.LogWarning("Complex field search called without complexFieldId");
                return new JsonResult(new List<object>());
            }

            try
            {
                var results = await autocompleteService.SearchAsync(complexFieldId, query);
                return new JsonResult(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in complex field search complexFieldId: {ComplexFieldId}, query: {Query}", complexFieldId, query);
                return new JsonResult(new List<object>());
            }
        }

        private FormEngineVisibilityEvaluator Visibility =>
            _visibility ??= new FormEngineVisibilityEvaluator(
                Template,
                ConditionalState,
                _conditionalLogicOrchestrator,
                CurrentPageId,
                TaskId,
                _logger);

        public bool IsFieldHidden(string fieldId) => Visibility.IsFieldHidden(fieldId);

        public bool IsPageHidden(string pageId) => Visibility.IsPageHidden(pageId);

        public void EnsureItemFieldVisibility(Dictionary<string, object> itemData, IEnumerable<string> fieldIds) =>
            Visibility.EnsureItemFieldVisibility(itemData, fieldIds);

        public bool IsFieldHiddenForItem(string fieldId, Dictionary<string, object> itemData) =>
            Visibility.IsFieldHiddenForItem(fieldId, itemData);

        public async Task<IActionResult> OnPostUploadFileAsync()
        {
            try
            {
                await CommonFormEngineInitializationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Upload initialization failed; continuing with posted file context");
            }

            var applicationId = Request.Form["ApplicationId"].ToString();
            var fieldId = Request.Form["FieldId"].ToString();
            var returnUrl = Request.Form["ReturnUrl"].ToString();
            var uploadDescription = Request.Form["UploadDescription"].ToString();

            if (!IsCollectionFlow)
            {
                ModelState.Remove("FlowId");
                ModelState.Remove("InstanceId");
            }

            if (!Guid.TryParse(applicationId, out var appId))
                return NotFound();

            var file = Request.Form.Files["UploadFile"];
            var hasFile = file is { Length: > 0 };
            await using var stream = hasFile ? file!.OpenReadStream() : Stream.Null;

            var state = CaptureWorkState();
            var outcome = await _uploadFormFile.ExecuteAsync(state, new UploadFormFileRequest(
                appId,
                fieldId,
                returnUrl,
                uploadDescription,
                stream,
                file?.FileName ?? string.Empty,
                file?.ContentType,
                $"{ReferenceNumber}_{TaskId}_{CurrentPageId}",
                hasFile));
            ApplyWorkState(state);
            await TryNotifyFileOperationAsync(outcome);
            return MapOutcome(outcome);
        }

        public async Task<IActionResult> OnPostDownloadFileAsync()
        {
            var applicationId = Request.Form["ApplicationId"].ToString();
            var fileIdStr = Request.Form["FileId"].ToString();

            if (!Guid.TryParse(applicationId, out var appId) || !Guid.TryParse(fileIdStr, out var fileId))
                return NotFound();

            var state = CaptureWorkState();
            var outcome = await _downloadFormFile.ExecuteAsync(state, new DownloadFormFileRequest(appId, fileId));
            ApplyWorkState(state);
            return MapOutcome(outcome);
        }

        public async Task<IActionResult> OnPostDeleteFileAsync()
        {
            ModelState.Clear();

            var applicationId = Request.Form["ApplicationId"].ToString();
            var returnUrl = Request.Form["ReturnUrl"].ToString();
            var fileIdStr = Request.Form["FileId"].ToString();
            var fieldId = Request.Form["FieldId"].ToString();

            if (!Guid.TryParse(applicationId, out var appId))
                return NotFound();

            if (!Guid.TryParse(fileIdStr, out var fileId))
            {
                ModelState.AddModelError("FileId", FormEngineMessages.InvalidFileId);
                return string.IsNullOrEmpty(returnUrl) ? Page() : Redirect(returnUrl);
            }

            var confirmed = Request.Query.ContainsKey("confirmed") && Request.Query["confirmed"] == "true";
            var state = CaptureWorkState();
            var outcome = await _deleteFormFile.ExecuteAsync(
                state,
                new DeleteFormFileRequest(appId, fileId, fieldId, returnUrl, confirmed));
            ApplyWorkState(state);
            await TryNotifyFileOperationAsync(outcome);
            return MapOutcome(outcome);
        }

        private async Task TryNotifyFileOperationAsync(FormEngineOutcome outcome)
        {
            if (string.IsNullOrEmpty(outcome.SuccessMessage) || string.IsNullOrEmpty(outcome.NotificationContext))
                return;

            await TryCreateFileNotificationAsync(new AddNotificationRequest
            {
                Message = outcome.SuccessMessage,
                Category = "file-upload",
                Context = outcome.NotificationContext,
                Type = NotificationType.Success,
                AutoDismiss = false,
                AutoDismissSeconds = outcome.NotificationContext.StartsWith("file-upload|", StringComparison.Ordinal)
                    ? 5
                    : 0,
                ReplaceExistingContext = false
            });
        }

        private async Task TryCreateFileNotificationAsync(AddNotificationRequest addRequest)
        {
            try
            {
                addRequest.Context = NotificationScopeContext.PrefixDetail(ApplicationContext, addRequest.Context);
                await _notificationsClient.CreateNotificationAsync(addRequest);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "File operation succeeded but notification could not be created");
            }
        }

        private void RestoreFormErrors()
        {
            try
            {
                var contextKey = $"{ReferenceNumber}_{TaskId}_{CurrentPageId}";
                var (fieldErrors, generalError) = _formErrorStore.Load(contextKey, clearAfterRead: true);

                if (fieldErrors.Count > 0)
                {
                    foreach (var kvp in fieldErrors)
                    {
                        foreach (var error in kvp.Value)
                            ModelState.AddModelError(kvp.Key, error);
                    }
                    _logger.LogInformation(
                        "DEBUG: Restored {ErrorCount} field errors from FormErrorStore with key: {ContextKey}",
                        fieldErrors.Sum(x => x.Value.Count),
                        contextKey);
                }

                if (!string.IsNullOrEmpty(generalError))
                {
                    ModelState.AddModelError("", generalError);
                    _logger.LogInformation("DEBUG: Restored general error from FormErrorStore: {GeneralError}", generalError);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore form errors from session");
            }
        }
    }
}
