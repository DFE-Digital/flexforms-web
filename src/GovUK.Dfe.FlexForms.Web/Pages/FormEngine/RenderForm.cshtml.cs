using GovUK.Dfe.FlexForms.Application.Exceptions;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Notifications;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Extensions;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using GovUK.Dfe.FlexForms.Web.Constants;
using GovUK.Dfe.FlexForms.Web.Interfaces;
using GovUK.Dfe.FlexForms.Web.Pages.Shared;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using static GovUK.Dfe.FlexForms.Web.Pages.FormEngine.DisplayHelpers;
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
        IFileUploadService fileUploadService,
        IApplicationsClient applicationsClient,
        IConditionalLogicOrchestrator conditionalLogicOrchestrator,
        INotificationsClient notificationsClient,
        IFormErrorStore formErrorStore,
        IComplexFieldConfigurationService complexFieldConfigurationService,
        IDerivedCollectionFlowService derivedCollectionFlowService,
        IFieldRequirementService fieldRequirementService,
        ICollectionFlowProgressStore collectionFlowProgressStore,
        IInfectedUploadFilter infectedUploadFilter,
        IFormFileFieldService formFileFieldService,
        IPostedFormDataBinder postedFormDataBinder,
        IFormEnginePresentationComposer formEnginePresentationComposer,
        ILogger<RenderFormModel> logger,
        INavigationHistoryService navigationHistoryService,
        IRequestAppConfiguration requestConfiguration)
        : BaseFormEngineModel(renderer, applicationResponseService, fieldFormattingService, templateManagementService,
            applicationStateService, formStateManager, formNavigationService, formDataManager, formValidationOrchestrator, formConfigurationService, logger)
    {
        private readonly IApplicationsClient _applicationsClient = applicationsClient;
        private readonly IConditionalLogicOrchestrator _conditionalLogicOrchestrator = conditionalLogicOrchestrator;
        private readonly INotificationsClient _notificationsClient = notificationsClient;
        private readonly IFormErrorStore _formErrorStore = formErrorStore;
        private readonly IComplexFieldConfigurationService _complexFieldConfigurationService = complexFieldConfigurationService;
        private readonly IDerivedCollectionFlowService _derivedCollectionFlowService = derivedCollectionFlowService;
        private readonly ICollectionFlowProgressStore _collectionFlowProgressStore = collectionFlowProgressStore;
        private readonly IInfectedUploadFilter _infectedUploadFilter = infectedUploadFilter;
        private readonly IFormFileFieldService _formFileFieldService = formFileFieldService;
        private readonly IPostedFormDataBinder _postedFormDataBinder = postedFormDataBinder;
        private readonly IFormEnginePresentationComposer _formEnginePresentationComposer = formEnginePresentationComposer;
        private readonly IFieldRequirementService _fieldRequirementService = fieldRequirementService;
        private readonly INavigationHistoryService _navigationHistoryService = navigationHistoryService;
        private readonly IRequestAppConfiguration _requestConfiguration = requestConfiguration;
        private string ApplicationContext =>
            _requestConfiguration["ApplicationName"]
            ?? _requestConfiguration["TenantName"]
            ?? "platform";

        [BindProperty(SupportsGet = false)] public Dictionary<string, object> Data { get; set; } = new();

        public string BackLinkUrl => GetBackLinkUrl();

        [BindProperty] public bool IsTaskCompleted { get; set; }
        
        // Collection flow properties from form submission
        [BindProperty] public new string? FlowId { get; set; }
        [BindProperty] public new string? InstanceId { get; set; }
        [BindProperty] public string? FlowPageId { get; set; }
        
        // Derived collection flow properties
        [BindProperty] public string? DerivedFlowId { get; set; }
        [BindProperty] public string? DerivedItemId { get; set; }
        [BindProperty] public string? DerivedPageId { get; set; }
        
        // Calculate IsCollectionFlow automatically based on FlowId and InstanceId presence
        private bool IsCollectionFlow => !string.IsNullOrEmpty(FlowId) && !string.IsNullOrEmpty(InstanceId);
        
        // Calculate IsDerivedFlow automatically based on DerivedFlowId and DerivedItemId presence
        private bool IsDerivedFlow => !string.IsNullOrEmpty(DerivedFlowId) && !string.IsNullOrEmpty(DerivedItemId);

        // Success message for collection operations
        [TempData] public string? SuccessMessage { get; set; }
        
        // Error message for upload operations
        [TempData] public string? ErrorMessage { get; set; }
        
        // Files property for upload field (matches original UploadFile.cshtml.cs)
        public IReadOnlyList<UploadDto> Files { get; set; } = new List<UploadDto>();

        public bool FileValidationBlocksSubmit { get; set; }

        public IReadOnlyList<FileValidationBlockDto> FileValidationBlockingFiles { get; set; } = [];

        public ApplicationPreviewViewModel? Preview { get; private set; }

        public IReadOnlyList<CollectionFlowSectionViewModel> CollectionFlows { get; private set; } = [];

        // Conditional logic state for the current form
        public FormConditionalState? ConditionalState { get; set; }

        /// <summary>
        /// Per-request cache: one lean field-visibility evaluation per collection item dictionary
        /// (preview/summary previously re-ran the whole template for every column).
        /// </summary>
        private readonly Dictionary<object, FormConditionalState> _itemConditionalStateCache =
            new(ReferenceEqualityComparer.Instance);

        private HashSet<string>? _fieldsWithConditionalVisibility;

        public async Task OnGetAsync()
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

                // Ensure Template is not null to prevent NullReferenceException
                if (Template == null)
                {
                    _logger.LogError("Template is null after CommonFormEngineInitializationAsync for ReferenceNumber: {ReferenceNumber}", ReferenceNumber);
                    Template = new FormTemplate
                    {
                        TemplateId = "dummy",
                        TemplateName = "dummy",
                        Description = "dummy",
                        TaskGroups = new List<TaskGroup>()
                    };
                }
                else
                {
                    
                }

                // Check if this is a preview request
                if (Request.Query.ContainsKey("preview"))
                {
                    // Override the form state for preview requests
                    CurrentFormState = FormState.ApplicationPreview;
                    CurrentGroup = null;
                    CurrentTask = null;
                    CurrentPage = null;
                    
                    // Clear all validation errors for preview since we don't need validation on preview page
                    ModelState.Clear();
                    await RefreshFileValidationGateAsync();
                }
                else
                {
                    // Detect sub-flow route segments inside pageId via route value parsing if needed in future
                    // If application is not editable and trying to access a specific page, redirect to preview
                    if (!IsApplicationEditable() && !string.IsNullOrEmpty(CurrentPageId))
                    {
                        Response.Redirect($"~/applications/{ReferenceNumber}");
                        return;
                    }

                    if (!string.IsNullOrEmpty(CurrentPageId))
                    {
                        if (TryParseFlowRoute(CurrentPageId, out var flowId, out var instanceId, out var flowPageId))
                        {

                            FlowId = flowId;
                            InstanceId = instanceId;
                            FlowPageId = flowPageId;
                            
                            // Sub-flow: initialize task and resolve page from task's pages
                            var (group, task) = InitializeCurrentTask(TaskId);
                            CurrentGroup = group;
                            CurrentTask = task;

                            // Find the correct flow and its pages
                            var flowPages = GetFlowPages(task, flowId);
                            var flowFieldId = GetFlowFieldId(task, flowId);

                            // Record whether the item existed before this flow started so we can choose the correct
                            // success message even after partial autosaves add the item to the session.
                            if (!string.IsNullOrEmpty(flowFieldId))
                            {
                                var existenceKey = FormSessionKeys.FlowItemExisted(flowId, instanceId);
                                if (HttpContext.Session.GetString(existenceKey) == null)
                                {
                                    var existed = IsExistingCollectionItem(flowFieldId, instanceId);
                                    HttpContext.Session.SetString(existenceKey, existed ? "true" : "false");
                                }
                            }
                            if (flowPages != null)
                            {
                                var page = FormStepPolicy.ResolvePage(flowPages, flowPageId);
                                if (page != null)
                                {
                                    CurrentPage = page;
                                    CurrentFormState = FormState.FormPage; // Render as a normal page
                                    
                                    // If editing existing item, load its data into form fields
                                    // This must happen AFTER LoadAccumulatedDataFromSession is skipped for sub-flows
                                    LoadExistingFlowItemData(flowId, instanceId);
                                    
                                    // Also load any in-progress data for this specific flow instance
                                // IMPORTANT: Progress data takes priority over existing item data as it contains the latest user changes
                                    var progressData = _collectionFlowProgressStore.Load(flowId, instanceId);
                                    foreach (var kvp in progressData)
                                    {
                                    Data[kvp.Key] = kvp.Value; // Always overwrite with progress data (latest changes)
                                }
                                

                                


                            }
                        }
                        }
                        else if (TryParseDerivedFlowRoute(CurrentPageId, out var derivedFlowId, out var derivedItemId, out var derivedPageId))
                        {
                            // Derived flow: initialize task and resolve page from derived flow configuration
                            var (group, task) = InitializeCurrentTask(TaskId);
                            CurrentGroup = group;
                            CurrentTask = task;

                            // Set derived flow properties
                            DerivedFlowId = derivedFlowId;
                            DerivedItemId = derivedItemId;
                            DerivedPageId = derivedPageId;

                            // Find the derived flow configuration
                            var derivedConfig = GetDerivedFlowConfiguration(task, derivedFlowId);
                            if (derivedConfig != null)
                            {
                                // Get the page to render (default to first page if no specific page)
                                var page = FormStepPolicy.ResolvePage(derivedConfig.Pages, derivedPageId);
                                if (page != null)
                                {
                                    CurrentPage = page;
                                    CurrentFormState = FormState.FormPage;
                                    
                                    // Load pre-filled data for this derived item
                                    LoadDerivedItemData(derivedConfig, derivedItemId);

                                    // Replace placeholders in page metadata with the item's display name
                                    var displayName = GetDerivedItemDisplayName(derivedConfig, derivedItemId);
                                    if (!string.IsNullOrEmpty(CurrentPage.Title))
                                    {
                                        CurrentPage.Title = CurrentPage.Title
                                            .Replace("{displayName}", displayName)
                                            .Replace("{name}", displayName);
                                    }
                                    if (!string.IsNullOrEmpty(CurrentPage.Description))
                                    {
                                        CurrentPage.Description = CurrentPage.Description
                                            .Replace("{displayName}", displayName)
                                            .Replace("{name}", displayName);
                                    }
                                }
                            }
                        }
                        else
                        {
                            var (group, task, page) = InitializeCurrentPage(CurrentPageId);
                            CurrentGroup = group;
                            CurrentTask = task;
                            CurrentPage = page;
                        }
                    }
                    else if (!string.IsNullOrEmpty(TaskId))
                    {
                        var (group, task) = InitializeCurrentTask(TaskId);
                        CurrentGroup = group;
                        CurrentTask = task;
                        CurrentPage = null; // No specific page for task summary

                        // If task requests collectionFlow summary, switch state accordingly
                        if (_formStateManager.ShouldShowCollectionFlowSummary(CurrentTask))
                        {
                            CurrentFormState = FormState.TaskSummary; // view chooses partial
                        }
                        // If task requests derivedCollectionFlow summary, switch state accordingly
                        else if (_formStateManager.ShouldShowDerivedCollectionFlowSummary(CurrentTask))
                        {
                            CurrentFormState = FormState.DerivedCollectionFlowSummary;
                        }
                    }
                }

                // Check if we need to clear session data for a new application
                CheckAndClearSessionForNewApplication();

                await LoadAccumulatedDataFromSessionAsync();
                MergeFlowProgressIntoFormDataForSummary();

                // For derived flow pages, re-apply declaration data AFTER accumulated data.
                // The accumulated session may contain stale top-level keys (e.g. "chairName-joining")
                // that were saved before derived-flow isolation. The declaration data in
                // "fieldId_data_itemId" holds the authoritative values and must take priority.
                if (!string.IsNullOrEmpty(DerivedFlowId) && !string.IsNullOrEmpty(DerivedItemId) && CurrentTask != null)
                {
                    var derivedConfig = GetDerivedFlowConfiguration(CurrentTask, DerivedFlowId);
                    if (derivedConfig != null)
                    {
                        LoadDerivedItemData(derivedConfig, DerivedItemId);
                    }
                }

                // For upload fields, populate Data from session so they display on GET
                // This ensures files appear in the list after upload
                PopulateUploadFieldsFromSession();
                
                await ApplyConditionalLogicAsync();
                ModelState.Clear();
                RestoreFormErrors();
                
                ViewData["ValidationErrors"] = ModelState.Where(m => m.Value.Errors.Any())
                    .ToDictionary(m => m.Key, m => m.Value.Errors.Select(e => e.ErrorMessage).ToList());

                // Initialize task completion status for summaries (standard or derived)
                if (CurrentTask != null)
                {
                    var isSummary = CurrentFormState == FormState.TaskSummary 
                        || _formStateManager.ShouldShowDerivedCollectionFlowSummary(CurrentTask);
                    if (isSummary)
                    {
                        var taskStatus = GetTaskStatusFromSession(CurrentTask.TaskId);
                        IsTaskCompleted = taskStatus == Domain.Models.TaskStatus.Completed;
                        
                        // Clear any validation errors when viewing task summary on GET
                        // Task completion validation errors should only appear after POST, not on initial load
                        ModelState.Clear();
                    }
                }
            // If this GET was reached via back navigation, pop history entry for the current scope
            try
            {
                if (Request.Query.ContainsKey("nav") && string.Equals(Request.Query["nav"], "back", StringComparison.OrdinalIgnoreCase))
                {
                    var scope = BuildHistoryScope(ReferenceNumber, TaskId, CurrentPageId);
                    _navigationHistoryService.Pop(scope);
                }
            }
            catch { }
        }

        public override void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
            BuildPresentationViewModels();
            base.OnPageHandlerExecuted(context);
        }

        public static string BuildHistoryScope(string referenceNumber, string taskId, string currentPageId) =>
            FormRouteParser.HistoryScope(referenceNumber, taskId, currentPageId);

        private void BuildPresentationViewModels()
        {
            if (Template == null)
                return;

            var presentationContext = CreatePresentationContext();

            if (CurrentFormState == FormState.ApplicationPreview)
            {
                Preview = _formEnginePresentationComposer.BuildPreview(presentationContext);
            }

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

        private bool IsCurrentUserLeadApplicant()
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

            // Initialize the current task for task summary
            if (!string.IsNullOrEmpty(TaskId))
            {
                var (group, task) = InitializeCurrentTask(TaskId);
                CurrentGroup = group;
                CurrentTask = task;
                CurrentPage = null;
            }

            // Task summary POST does not submit form field data, so Data is empty.
            // We need to apply conditional logic using FormData (session data) for accurate validation.
            // Create a custom conditional logic evaluation using FormData instead of Data.
            try
            {
                if (Template?.ConditionalLogic != null && Template.ConditionalLogic.Any())
                {
                    var context = new ConditionalLogicContext
                    {
                        CurrentPageId = CurrentPageId,
                        CurrentTaskId = TaskId,
                        IsClientSide = false,
                        Trigger = "task_summary_validation"
                    };

                    //Use FormData (session data) instead of Data (empty on task summary POST)
                    ConditionalState = await _conditionalLogicOrchestrator.ApplyConditionalLogicAsync(Template, FormData, context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying conditional logic in task summary validation");
                // Continue with empty conditional state - better than failing
                ConditionalState = new FormConditionalState();
            }
            
            // Log conditional state for debugging
            _logger.LogInformation("ConditionalState after ApplyConditionalLogicAsync: Fields={FieldCount}, HiddenFields={HiddenFields}", 
                ConditionalState?.FieldVisibility?.Count ?? 0,
                string.Join(", ", ConditionalState?.FieldVisibility?.Where(kv => !kv.Value).Select(kv => kv.Key) ?? new List<string>()));

            // Handle task completion checkbox state
            if (CurrentTask != null && ApplicationId.HasValue)
            {
                if (IsTaskCompleted)
                {
                    // Use new method that returns custom error messages
                    var missingFieldsWithMessages = _fieldRequirementService.GetMissingRequiredFieldsWithMessages(CurrentTask, Template, FormData, IsFieldHidden);
                    var errorLines = new List<string>();

                    if (missingFieldsWithMessages.Any())
                    {
                        foreach (var errorMessage in missingFieldsWithMessages.Values)
                        {
                            errorLines.Add(errorMessage);
                        }
                    }

                    // Additional validation for multi-collection flow tasks
                    if (CurrentTask.Summary?.Mode?.Equals("multiCollectionFlow", StringComparison.OrdinalIgnoreCase) == true &&
                        CurrentTask.Summary.Flows != null && CurrentTask.Summary.Flows.Any())
                    {
                        foreach (var flow in CurrentTask.Summary.Flows)
                        {
                            var items = ReadCollectionItemsFromFormData(flow.FieldId);
                            var itemCount = items.Count;

                            var requiredMin = flow.MinItems ?? 1; // default to at least one item
                            if (itemCount < requiredMin)
                            {
                                var flowTitle = string.IsNullOrWhiteSpace(flow.Title)
                                    ? (string.IsNullOrWhiteSpace(CurrentTask?.TaskName) ? "this section" : CurrentTask!.TaskName)
                                    : flow.Title;
                                errorLines.Add($"• Add at least {requiredMin} item(s) to {flowTitle}");
                                _logger.LogInformation("Collection flow '{FlowId}' requires at least {MinItems} items but has {Count}", flow.FlowId, requiredMin, itemCount);
                            }

                            // Check each collection item has all required fields completed
                            if (flow.Pages != null && items.Any())
                            {
                                foreach (var item in items)
                                {
                                    bool itemHasMissingFields = false;
                                    var requiredFieldIds = flow.Pages
                                        .Where(p => p?.Fields != null)
                                        .SelectMany(p => p.Fields)
                                        .Where(f => _fieldRequirementService.IsFieldRequired(f, Template))
                                        .Select(f => f.FieldId)
                                        .ToList();
                                    EnsureItemFieldVisibility(item, requiredFieldIds);

                                    foreach (var page in flow.Pages)
                                    {
                                        if (page?.Fields == null) continue;
                                        foreach (var field in page.Fields)
                                        {
                                            if (!_fieldRequirementService.IsFieldRequired(field, Template)) continue;

                                            if (IsFieldHiddenForItem(field.FieldId, item)) continue;

                                            var hasValue = item.TryGetValue(field.FieldId, out var val)
                                                           && val != null
                                                           && !string.IsNullOrWhiteSpace(val.ToString());
                                            if (!hasValue)
                                            {
                                                itemHasMissingFields = true;
                                                break;
                                            }
                                        }
                                        if (itemHasMissingFields) break;
                                    }

                                    if (itemHasMissingFields)
                                    {
                                        var flowTitle = string.IsNullOrWhiteSpace(flow.Title)
                                            ? (string.IsNullOrWhiteSpace(CurrentTask?.TaskName) ? "this section" : CurrentTask!.TaskName)
                                            : flow.Title;
                                        errorLines.Add($"Complete all required questions for each item in {flowTitle}");
                                        _logger.LogInformation("Collection flow '{FlowId}' has an item with incomplete required fields", flow.FlowId);
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (errorLines.Any())
                    {
                        // Cannot complete task - required fields are missing
                        ModelState.Clear();

                        // Create error message with bullet points
                        var errorMessage = "You cannot mark this section as complete because some required questions have not been answered:\n" +
                                         string.Join("\n", errorLines);
                        
                        ModelState.AddModelError(string.Empty, errorMessage);
                        
                        IsTaskCompleted = false; // Reset the checkbox state
                        
                        //  Set CurrentFormState so the view knows to render the task summary
                        CurrentFormState = FormState.TaskSummary;
                        
                        // DON'T save ModelState errors to FormErrorStore - they should only appear once
                        // on this immediate response, not persist to next GET request
                        return Page();
                    }
                    
                    // Mark the task as completed in session and API
                    await _applicationStateService.SaveTaskStatusAsync(ApplicationId.Value, CurrentTask.TaskId, Domain.Models.TaskStatus.Completed);
                }
                else
                {
                    // Task was unchecked - set it back to in progress if it has data, otherwise not started
                    var currentStatus = _applicationStateService.CalculateTaskStatus(CurrentTask.TaskId, Template, FormData, ApplicationId, ApplicationStatus);
                    if (currentStatus == Domain.Models.TaskStatus.Completed)
                    {
                        // Only override if it was explicitly marked as completed - revert to calculated status
                        var calculatedStatus = HasAnyTaskData(CurrentTask) ? Domain.Models.TaskStatus.InProgress : Domain.Models.TaskStatus.NotStarted;
                        await _applicationStateService.SaveTaskStatusAsync(ApplicationId.Value, CurrentTask.TaskId, calculatedStatus);
                    }
                }
            }

            // Redirect to the task list page
            return Redirect($"/applications/{ReferenceNumber}");
        }

        public async Task<IActionResult> OnPostSubmitApplicationAsync()
        {
            // Clear any model state errors for route parameters since they're not relevant for preview submission
            ModelState.Remove(nameof(TaskId));
            ModelState.Remove(nameof(CurrentPageId));
            ModelState.Remove("TaskId");
            ModelState.Remove("CurrentPageId");
            ModelState.Remove("pageId");
            ModelState.Remove("taskId");
            
            // Initialize common form engine data first (loads Template, FormData, etc.)
            await CommonFormEngineInitializationAsync();

            // Prevent submission if application is not editable
            if (!IsApplicationEditable())
            {
                CurrentFormState = FormState.ApplicationPreview;
                ModelState.AddModelError("", ApplicationAccessMessages.NoWritePermission);
                return Page();
            }

            // Check if all tasks are completed before allowing submission
            if (!AreAllTasksCompleted())
            {
                _logger.LogWarning("Cannot submit application {ReferenceNumber} - not all tasks completed", ReferenceNumber);
                
                // Override the form state for preview with errors
                CurrentFormState = FormState.ApplicationPreview;
                
                ModelState.AddModelError("", "All sections must be completed before you can submit your application.");
                return Page();
            }

            // Additional validation: Check that all required fields actually have values
            // This catches cases where files were removed by virus scanner after task was marked complete
            var tasksWithMissingFields = ValidateAllRequiredFieldsForSubmission(IsFieldHidden);
            if (tasksWithMissingFields.Any())
            {
                _logger.LogWarning(
                    "Cannot submit application {ReferenceNumber} - {TaskCount} task(s) have missing required fields: {TaskIds}",
                    ReferenceNumber,
                    tasksWithMissingFields.Count,
                    string.Join(", ", tasksWithMissingFields.Keys));
                
                // Override the form state for preview with errors
                CurrentFormState = FormState.ApplicationPreview;
                
                // Find task names for better error message
                var taskNames = tasksWithMissingFields.Keys
                    .Select(taskId => Template?.TaskGroups?
                        .SelectMany(g => g.Tasks)
                        .FirstOrDefault(t => t.TaskId == taskId)?.TaskName ?? taskId)
                    .ToList();
                
                ModelState.AddModelError("", 
                    $"Some sections have missing required information and need to be completed again: {string.Join(", ", taskNames)}");
                return Page();
            }

            if (!ApplicationId.HasValue)
            {
                _logger.LogError("ApplicationId not found during submission for reference {ReferenceNumber}", ReferenceNumber);
                ModelState.AddModelError("", "Application not found. Please try again.");
                return Page();
            }

            await RefreshFileValidationGateAsync();
            if (FileValidationBlocksSubmit)
            {
                CurrentFormState = FormState.ApplicationPreview;
                var names = string.Join(", ", FileValidationBlockingFiles.Select(f => f.OriginalFileName));
                ModelState.AddModelError("",
                    $"Some uploaded files failed validation or are still being checked: {names}");
                return Page();
            }

            try
            {
                _logger.LogInformation("Attempting to submit application {ApplicationId} with reference {ReferenceNumber}", 
                    ApplicationId.Value, ReferenceNumber);

                // Submit the application via API
                var submittedApplication = await _applicationsClient.SubmitApplicationAsync(ApplicationId.Value);
                
                // Update session with new application status
                if (submittedApplication != null)
                {
                    var statusKey = $"ApplicationStatus_{ApplicationId.Value}";
                    HttpContext.Session.SetString(statusKey, submittedApplication.Status?.ToString() ?? "Submitted");
                    // Outbound mapped events are published by the API from its
                    // ApplicationSubmitted domain event, using the tenant's EventTriggers.
                    _logger.LogInformation("Successfully submitted application {ApplicationId} with reference {ReferenceNumber}",
                        ApplicationId.Value, ReferenceNumber);
                }
                else
                {
                    _logger.LogWarning("Submit API returned null for application {ApplicationId}", ApplicationId.Value);
                }
                
                return RedirectToPage("/Applications/ApplicationSubmitted", new { referenceNumber = ReferenceNumber });
            }
            catch (ExternalApplicationsException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit application {ApplicationId} with reference {ReferenceNumber}", 
                    ApplicationId.Value, ReferenceNumber);
                
                ModelState.AddModelError("", $"An error occurred while submitting your application: {ex.Message}. Please try again.");
                CurrentFormState = FormState.ApplicationPreview;
                return Page();
            }
        }

        public async Task<IActionResult> OnPostPageAsync()
        {
            _logger.LogInformation("POST: OnPostPageAsync called - ReferenceNumber='{ReferenceNumber}', TaskId='{TaskId}', CurrentPageId='{CurrentPageId}'", 
                ReferenceNumber, TaskId, CurrentPageId);
            _logger.LogInformation("POST: Request URL: {Url}", $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}");
            _logger.LogInformation("POST: Form data keys: {Keys}", string.Join(", ", Request.Form.Keys));
            
            // This handler is also used by task summary pages which do not post a pageId.
            // Non-nullable reference types are implicitly required in MVC, so clear any implicit
            // model state error for missing pageId to avoid short-circuiting to Page().
            ModelState.Remove(nameof(CurrentPageId));
            ModelState.Remove("pageId");
            
            // Check if this is a confirmed action coming back from confirmation page
            if (Request.Query.ContainsKey("confirmed") && Request.Query["confirmed"] == "true")
            {
                // Restore the original form data from TempData
                var confirmedDataJson = TempData["ConfirmedFormData"]?.ToString();
                var confirmedHandler = TempData["ConfirmedHandler"]?.ToString();
                
                if (!string.IsNullOrEmpty(confirmedDataJson))
                {
                    try
                    {
                        var confirmedData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(confirmedDataJson);
                        if (confirmedData != null)
                        {
                            // Merge confirmed data into current Data
                            foreach (var kvp in confirmedData)
                            {
                                Data[kvp.Key] = kvp.Value;
                            }
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
            {
                CurrentPageId = System.Web.HttpUtility.UrlDecode(CurrentPageId);

            }

            if (!string.IsNullOrEmpty(CurrentPageId))
            {
                if (TryParseFlowRoute(CurrentPageId, out var flowId, out var instanceId, out var flowPageId))
                {
                    

                    var (group, task) = InitializeCurrentTask(TaskId);
                    CurrentGroup = group;
                    CurrentTask = task;

                    // Find the correct flow and its pages
                    var flowPages = GetFlowPages(task, flowId);
                    if (flowPages != null)
                    {
                        var page = FormStepPolicy.ResolvePage(flowPages, flowPageId);
                        if (page != null)
                        {
                            CurrentPage = page;
                        }
                    }
                }
                else if (TryParseDerivedFlowRoute(CurrentPageId, out var dFlowId, out var dItemId, out var dPageId))
                {
                    var (group, task) = InitializeCurrentTask(TaskId);
                    CurrentGroup = group;
                    CurrentTask = task;

                    var derivedConfig = GetDerivedFlowConfiguration(task, dFlowId);
                    if (derivedConfig != null)
                    {
                        var page = FormStepPolicy.ResolvePage(derivedConfig.Pages, dPageId);
                        if (page != null)
                        {
                            CurrentPage = page;
                        }
                    }
                }
                else
                {
            var (group, task, page) = InitializeCurrentPage(CurrentPageId);
            CurrentGroup = group;
            CurrentTask = task;
            CurrentPage = page;
                }
            }
            else if (!string.IsNullOrEmpty(TaskId))
            {
                // No pageId posted (e.g., task summary/derived summary). Initialize the task context.
                var (group, task) = InitializeCurrentTask(TaskId);
                CurrentGroup = group;
                CurrentTask = task;
                CurrentPage = null;
                _logger.LogInformation("POST: Initialized CurrentTask '{TaskId}' for summary POST (no pageId)", CurrentTask?.TaskId);
            }
            else if (!string.IsNullOrEmpty(TaskId))
            {
                var (group, task) = InitializeCurrentTask(TaskId);
                CurrentGroup = group;
                CurrentTask = task;
                CurrentPage = null; // No specific page for task summary
            }

            if (!IsApplicationEditable())
            {
                ModelState.AddModelError("", ApplicationAccessMessages.NoWritePermission);
                return Page();
            }

            var postedFields = Request.Form.ToPostedFields();
            Data = _postedFormDataBinder.Bind(postedFields, Data);
            _formFileFieldService.ReplaceUploadPlaceholders(Data, FileFieldContext);

            await ApplyConditionalLogicAsync("change");

            _postedFormDataBinder.ApplyDateParts(postedFields, Data);

			bool isDerivedFlowRoute = TryParseDerivedFlowRoute(CurrentPageId, out var _, out var _, out var _);
			if (!isDerivedFlowRoute && CurrentPage != null)
			{
				ValidateCurrentPage(CurrentPage, Data);
			}

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState invalid on POST Page");

                // (Reverted) Do not accumulate general invalid form data to session; sub-flow persistence below is sufficient
                
                // For sub-flow pages, persist latest values to flow progress prior to redirect
                try
                {
                    if (TryParseFlowRoute(CurrentPageId, out var fId, out var instId, out _))
                    {
                        _collectionFlowProgressStore.Save(fId, instId, Data);
                        _logger.LogInformation("Saved in-progress flow data for flow {FlowId}, instance {InstanceId} with {Count} fields due to validation errors.", fId, instId, Data?.Count ?? 0);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save flow progress on validation failure.");
                }

                var contextKey = GetFormErrorContextKey();
                _formErrorStore.Save(contextKey, ModelState);
                
                if (TryParseDerivedFlowRoute(CurrentPageId, out _, out _, out _))
                {
                    var selfUrl = $"/applications/{ReferenceNumber}/{TaskId}/{CurrentPageId}";
                    return Redirect(selfUrl);
                }

                if (TryParseFlowRoute(CurrentPageId, out _, out _, out _))
                {
                    var selfUrl = $"/applications/{ReferenceNumber}/{TaskId}/{CurrentPageId}";
                    return Redirect(selfUrl);
                }
                
                return Page();
            }


            // When AllowMultiple is true for an autocomplete complex field, append new selection
            // to any existing array value instead of replacing it
            if (CurrentPage != null)
            {
                try
                {
                    foreach (var field in CurrentPage.Fields.Where(f => f.Type == "complexField" && f.ComplexField != null))
                    {
                        var cfg = _complexFieldConfigurationService.GetConfiguration(field.ComplexField.Id);
                        if (!string.Equals(cfg.FieldType, "autocomplete", StringComparison.OrdinalIgnoreCase) || !cfg.AllowMultiple)
                        {
                            continue;
                        }

                        var key = field.FieldId;
                        if (!Data.TryGetValue(key, out var newValObj))
                        {
                            continue;
                        }

                        var newVal = newValObj?.ToString();
                        if (string.IsNullOrWhiteSpace(newVal))
                        {
                            continue;
                        }

                        // Load existing selections from accumulated session
                        var acc = _applicationResponseService.GetAccumulatedFormData();
                        var list = new List<object>();
                        if (acc.TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing?.ToString()))
                        {
                            var existingText = existing!.ToString()!;
                            var addedExisting = false;
                            // Try parse as array of objects
                            try
                            {
                                var parsedArray = JsonSerializer.Deserialize<List<object>>(existingText);
                                if (parsedArray != null)
                                {
                                    list = parsedArray;
                                    addedExisting = true;
                                }
                            }
                            catch { }

                            // If not an array, try parse as single object and add it as first element
                            if (!addedExisting)
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(existingText);
                                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                                    {
                                        list.Add(doc.RootElement.Clone());
                                        addedExisting = true;
                                    }
                                }
                                catch { }
                            }

                            // If still not added and it's a non-empty string, include as string element
                            if (!addedExisting && !string.IsNullOrWhiteSpace(existingText))
                            {
                                list.Add(existingText);
                            }
                        }

                        // Avoid duplicates by comparing JSON string
                        bool exists = false;
                        try
                        {
                            var newJson = newVal;
                            exists = list.Any(x => (x?.ToString() ?? "") == newJson);
                        }
                        catch { }

                        if (!exists)
                        {
                            try
                            {
                                using var newDoc = JsonDocument.Parse(newVal);
                                if (newDoc.RootElement.ValueKind == JsonValueKind.Object || newDoc.RootElement.ValueKind == JsonValueKind.Array)
                                {
                                    list.Add(newDoc.RootElement.Clone());
                                }
                                else if (newDoc.RootElement.ValueKind == JsonValueKind.String)
                                {
                                    list.Add(newDoc.RootElement.GetString() ?? string.Empty);
                                }
                                else
                                {
                                    list.Add(newDoc.RootElement.ToString());
                                }
                            }
                            catch
                            {
                                // If not JSON, store as string value
                                list.Add(newVal);
                            }
                        }

                        var updatedJson = JsonSerializer.Serialize(list);
                        // Update both normalized and Data_ forms to be safe
                        Data[key] = updatedJson;
                        Data[$"Data_{key}"] = updatedJson;
                        _applicationResponseService.AccumulateFormData(new Dictionary<string, object> { [key] = updatedJson });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to merge multi-select autocomplete values");
                }
            }

            // Save the current page data to the API (skip for sub-flows and derived flows as they accumulate data differently)
            bool isSubFlow = TryParseFlowRoute(CurrentPageId, out _, out _, out _);
            bool isDerivedFlowSave = TryParseDerivedFlowRoute(CurrentPageId, out _, out _, out _);
            if (ApplicationId.HasValue && Data.Any() && !isSubFlow && !isDerivedFlowSave)
            {
                await _applicationResponseService.SaveApplicationResponseAsync(ApplicationId.Value, Data);
                _logger.LogInformation("Successfully saved response for Application {ApplicationId}, Page {PageId}",
                    ApplicationId.Value, CurrentPageId);
            }

            // Before deciding where to go, push current page URL to navigation history so Back returns here
            try
            {
                if (!string.IsNullOrEmpty(CurrentPageId))
                {
                    var scope = RenderFormModel.BuildHistoryScope(ReferenceNumber, TaskId, CurrentPageId);
                    var currentUrl = $"/applications/{ReferenceNumber}/{TaskId}/{CurrentPageId}";
                    _navigationHistoryService.Push(scope, currentUrl);
                }
                else if (!string.IsNullOrEmpty(TaskId))
                {
                    var scope = RenderFormModel.BuildHistoryScope(ReferenceNumber, TaskId, CurrentPageId);
                    var currentUrl = $"/applications/{ReferenceNumber}/{TaskId}";
                    _navigationHistoryService.Push(scope, currentUrl);
                }
            }
            catch { }

            // Use the new navigation logic to determine where to go after saving
            if (CurrentTask != null && CurrentPage != null)
            {
                // If this is a sub-flow route, compute next page within the flow
                if (TryParseFlowRoute(CurrentPageId, out var flowId, out var instanceId, out var flowPageId))
                {
                    // Find the correct flow and its pages
                    var flowPages = GetFlowPages(CurrentTask, flowId);
                    var flowFieldId = GetFlowFieldId(CurrentTask, flowId);
                    
                    if (flowPages != null && !string.IsNullOrEmpty(flowFieldId))
                    {
                        // Use the existence flag captured when the flow was first opened (fallback to current check)
                        var existenceKey = FormSessionKeys.FlowItemExisted(flowId, instanceId);
                        bool itemExistedBeforeSave = HttpContext.Session.GetString(existenceKey) is { } existedValue &&
                                                     bool.TryParse(existedValue, out var parsed)
                                                     ? parsed
                                                     : IsExistingCollectionItem(flowFieldId, instanceId);

                        // Persist in-progress sub-flow data for this instance
                        _collectionFlowProgressStore.Save(flowId, instanceId, Data);

                        // Also persist partial collection item to the database on every page
                        if (ApplicationId.HasValue)
                        {
                            var accumulatedProgress = _collectionFlowProgressStore.Load(flowId, instanceId);
                            AppendCollectionItemToSession(flowPages, flowFieldId, instanceId, accumulatedProgress);

                            var accData = _applicationResponseService.GetAccumulatedFormData();
                            if (accData.TryGetValue(flowFieldId, out var collectionValue))
                            {
                                await _applicationResponseService.SaveApplicationResponseAsync(
                                    ApplicationId.Value,
                                    new Dictionary<string, object> { [flowFieldId] = collectionValue });
                                _logger.LogInformation("Saved partial collection item to database for flow {FlowId}, instance {InstanceId}, page {PageId}",
                                    flowId, instanceId, CurrentPageId);
                            }
                        }

                        var index = FormStepPolicy.IndexOfPage(flowPages, CurrentPage.PageId);
                        var isLast = FormStepPolicy.IsLastPage(flowPages, CurrentPage.PageId);
                        if (!isLast)
                        {
                            // Find the next visible page using conditional logic
                            string? nextPageId = null;
                            
                            // Check if we have conditional logic to determine next page
                            if (ConditionalState != null)
                            {
                                _logger.LogDebug("Sub-flow navigation: checking conditional logic for pages. Current page: {CurrentPageId}, Flow: {FlowId}", CurrentPage.PageId, flowId);
                                
                                // Re-evaluate conditional logic with complete flow data for navigation
                                var mergedData = _collectionFlowProgressStore.Load(FlowId, InstanceId);
                                foreach (var kvp in Data)
                                {
                                    mergedData[kvp.Key] = kvp.Value;
                                }
                                
                                
                                var navContext = new ConditionalLogicContext
                                {
                                    CurrentPageId = CurrentPageId,
                                    CurrentTaskId = TaskId,
                                    IsClientSide = false,
                                    Trigger = "change"
                                };
                                
                                // Re-compute conditional state with complete data
                                var updatedConditionalState = await _conditionalLogicOrchestrator.ApplyConditionalLogicAsync(Template, mergedData, navContext);
                                
                                
                                // Look for the next visible page after current page using updated state
                                for (int i = index + 1; i < flowPages.Count; i++)
                                {
                                    var candidatePage = flowPages[i];
                                    
                                    // Check if this page should be skipped due to conditional logic using updated state
                                    var isHidden = updatedConditionalState.PageVisibility.TryGetValue(candidatePage.PageId, out var isVisible) && !isVisible;
                                    var isSkipped = updatedConditionalState.SkippedPages.Contains(candidatePage.PageId);
                                    
                                    
                                    if (!isHidden && !isSkipped)
                                    {
                                        nextPageId = candidatePage.PageId;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                // Fallback to simple next page logic if no conditional logic
                                nextPageId = flowPages[index + 1].PageId;
                            }
                            
                            
                            if (!string.IsNullOrEmpty(nextPageId))
                            {
                                var nextUrl = _formNavigationService.GetSubFlowPageUrl(CurrentTask.TaskId, ReferenceNumber, flowId, instanceId, nextPageId);
                                return Redirect(nextUrl);
                            }
                            
                            // If no valid next page found, treat as last page and complete the flow
                            // Fall through to flow completion logic below
                        }
                        
                        // Flow completion logic - execute when no next page is found
                        // Flow complete: append item to collection and go back to collection summary
                        if (!string.IsNullOrEmpty(flowFieldId))
                        {
                            // Merge accumulated progress with final page data
                            var accumulated = _collectionFlowProgressStore.Load(flowId, instanceId);
            
                            foreach (var kv in Data)
                            {
                                // Do not overwrite existing upload data with placeholder token
                                if (kv.Value?.ToString() == "UPLOAD_FIELD_SESSION_DATA" && accumulated.ContainsKey(kv.Key))
                                {
                                    continue;
                                }
                                accumulated[kv.Key] = kv.Value;
                            }

                            AppendCollectionItemToSession(flowPages, flowFieldId, instanceId, accumulated);
                            
                            // Generate simple, consistent success message
                            var flow = CurrentTask.Summary?.Flows?.FirstOrDefault(f => f.FlowId == flowId);
                            var taskTitle = CurrentTask?.TaskName ?? flow?.Title ?? "Item";
                            SuccessMessage = $"{taskTitle} updated";
                            
                            if (ApplicationId.HasValue)
                            {
                                // Trigger save for the collection field
                                var acc = _applicationResponseService.GetAccumulatedFormData();
                                if (acc.TryGetValue(flowFieldId, out var collectionValue))
                                {
                                    await _applicationResponseService.SaveApplicationResponseAsync(ApplicationId.Value, new Dictionary<string, object> { [flowFieldId] = collectionValue });
                                }
                            }
                            // Clear the in-progress cache for this instance
                            _collectionFlowProgressStore.Clear(flowId, instanceId);

                            // Clear navigation history
                            var scope = BuildHistoryScope(ReferenceNumber, TaskId, CurrentPageId);
                            _navigationHistoryService.Clear(scope);
                        }
                        var backToSummary = _formNavigationService.GetCollectionFlowSummaryUrl(CurrentTask.TaskId, ReferenceNumber);
                        return Redirect(backToSummary);
                    }
                }
                
                _logger.LogInformation("POST: Checking if CurrentPageId '{CurrentPageId}' is a derived flow route", CurrentPageId);
                
                // Handle derived collection flow form submissions
                if (TryParseDerivedFlowRoute(CurrentPageId, out var derivedFlowId, out var derivedItemId, out var derivedPageId))
                {
                    _logger.LogInformation("POST: Detected derived flow route - flowId='{FlowId}', itemId='{ItemId}', pageId='{PageId}'", 
                        derivedFlowId, derivedItemId, derivedPageId);
                }
                else
                {
                    _logger.LogInformation("POST: CurrentPageId '{CurrentPageId}' is NOT a derived flow route", CurrentPageId);
                }
                
			if (TryParseDerivedFlowRoute(CurrentPageId, out derivedFlowId, out derivedItemId, out derivedPageId))
                {
				var correctTask = Template?.TaskGroups?.SelectMany(g => g.Tasks)?.FirstOrDefault(t => t.TaskId == TaskId);
				var derivedConfig = GetDerivedFlowConfiguration(correctTask, derivedFlowId);
				if (derivedConfig != null)
				{
					var currentDerivedPage = FormStepPolicy.ResolvePage(derivedConfig.Pages, derivedPageId);

					if (currentDerivedPage != null)
					{
						ValidateCurrentPage(currentDerivedPage, Data);
					}

					if (!ModelState.IsValid)
					{
						var contextKey = GetFormErrorContextKey();
						_formErrorStore.Save(contextKey, ModelState);
						var selfUrl = $"/applications/{ReferenceNumber}/{TaskId}/{CurrentPageId}";
						return Redirect(selfUrl);
					}
				}

                    if (derivedConfig != null)
                    {
                        // Save the declaration data and mark as signed
                        _derivedCollectionFlowService.SaveItemDeclaration(
                            derivedConfig.FieldId, 
                            derivedItemId, 
                            Data, 
                            "Signed", 
                            FormData);

                        // Save to API — only pass the declaration keys that were changed,
                        // not the entire stale FormData snapshot loaded at init time.
                        // This prevents overwriting the current session state with stale data
                        // which caused edits to not persist for existing (API-loaded) applications.
                        if (ApplicationId.HasValue)
                        {
                            var statusKey = $"{derivedConfig.FieldId}_status_{derivedItemId}";
                            var dataKey = $"{derivedConfig.FieldId}_data_{derivedItemId}";
                            var derivedUpdates = new Dictionary<string, object>
                            {
                                [statusKey] = FormData[statusKey],
                                [dataKey] = FormData[dataKey]
                            };
                            await _applicationResponseService.SaveApplicationResponseAsync(ApplicationId.Value, derivedUpdates);
                        }
                        else
                        {
                            _logger.LogWarning("DerivedFlow POST: No ApplicationId found, skipping API save");
                        }

                        // Generate success message
                        var displayName = GetDerivedItemDisplayName(derivedConfig, derivedItemId);
                        var templateMessage = derivedConfig.SignedMessage ?? "Declaration for {displayName} has been signed";
                        SuccessMessage = templateMessage
                            .Replace("{displayName}", displayName)
                            .Replace("{name}", displayName);
                        
                        

                        // Redirect back to derived collection summary
                        var redirectUrl = $"/applications/{ReferenceNumber}/{TaskId}";
                        
                        return Redirect(redirectUrl);
                    }
                    else
                    {
                        _logger.LogError("DerivedFlow POST: Could not find derived config for flowId='{FlowId}'", derivedFlowId);
                    }
                }
                else if (_formStateManager.ShouldShowDerivedCollectionFlowSummary(CurrentTask))
                {
                    // Handle POST from derived collection flow summary page (Continue button)
                    
                    
                    // Handle task completion checkbox and redirect to task list
                    var completedValue = Request.Form["IsTaskCompleted"].ToString();
                    var isCompleted = !string.IsNullOrEmpty(completedValue) &&
                        (string.Equals(completedValue, "true", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(completedValue, "on", StringComparison.OrdinalIgnoreCase));
                    

                    if (isCompleted)
                    {
                        // Persist a flag so API has an audit of completion action
                        await _applicationResponseService.SaveApplicationResponseAsync(ApplicationId.Value, new Dictionary<string, object>
                        {
                            [$"{TaskId}_completed"] = true
                        });

                        // Also set the task status to Completed (matches TaskSummary behaviour)
                        if (CurrentTask != null)
                        {
                            await _applicationStateService.SaveTaskStatusAsync(
                                ApplicationId.Value,
                                CurrentTask.TaskId,
                                Domain.Models.TaskStatus.Completed);
                        }

                        _logger.LogInformation("POST: About to redirect to task list using RedirectToPage with ReferenceNumber: {ReferenceNumber}", ReferenceNumber);
                        return RedirectToPage("/FormEngine/RenderForm", new { referenceNumber = ReferenceNumber });
                    }
                    else
                    {
                        

                        // If unchecked: set task status based on calculated state (in progress if any data exists, else not started)
                        if (CurrentTask != null && ApplicationId.HasValue)
                        {
                            var hasAnyData = _applicationStateService.CalculateTaskStatus(CurrentTask.TaskId, Template, FormData, ApplicationId, ApplicationStatus) 
                                != Domain.Models.TaskStatus.NotStarted;
                            var newStatus = hasAnyData ? Domain.Models.TaskStatus.InProgress : Domain.Models.TaskStatus.NotStarted;
                            await _applicationStateService.SaveTaskStatusAsync(ApplicationId.Value, CurrentTask.TaskId, newStatus);
                            
                        }
                        
                        // Use RedirectToPage to ensure proper page model initialization
                        return RedirectToPage("/FormEngine/RenderForm", new { referenceNumber = ReferenceNumber });
                    }
                }
                else
                {
                    // First check if returnToSummaryPage is true and should be respected
                    if (CurrentPage.ReturnToSummaryPage)
                    {


                        
                        // Check if conditional logic suggests a different next page (override returnToSummaryPage)
                        string? conditionalNextPageId = null;
                        bool hasConditionalTrigger = false;
                        
                        if (ConditionalState != null && Template != null)
                        {
                        // FIXED: Check if conditional rules specifically show/reveal new pages, not just any trigger
                        hasConditionalTrigger = HasConditionalLogicShowingPages();

                        _logger.LogInformation("[FLOW DEBUG] ReturnToSummaryPage=true path - hasConditionalTrigger: {HasTrigger}, currentPageId: {PageId}", hasConditionalTrigger, CurrentPage.PageId);
                        
                        if (hasConditionalTrigger)
                        {
                            _logger.LogInformation("[FLOW DEBUG] Data before calling GetNextPageAsync:");
                            foreach (var kv in Data.Take(10))
                            {
                                _logger.LogInformation("[FLOW DEBUG] Data[{Key}] = {Value}", kv.Key, kv.Value?.ToString() ?? "null");
                            }

                            var context = new ConditionalLogicContext
                            {
                                CurrentPageId = CurrentPageId,
                                CurrentTaskId = TaskId,
                                IsClientSide = false,
                                Trigger = "change"
                            };
                            
                            conditionalNextPageId = await _conditionalLogicOrchestrator.GetNextPageAsync(Template, Data, CurrentPage.PageId, context);
                            _logger.LogInformation("[FLOW DEBUG] GetNextPageAsync returned: {NextPageId}", conditionalNextPageId ?? "null");
                        }
                        }
                        
                        // If conditional logic found a next page AND was triggered, navigate there (override returnToSummaryPage)
                        if (hasConditionalTrigger && !string.IsNullOrEmpty(conditionalNextPageId))
                        {
                            var nextUrl = $"/applications/{ReferenceNumber}/{CurrentTask.TaskId}/{conditionalNextPageId}";

                            return Redirect(nextUrl);
                        }

                        // No conditional override - respect returnToSummaryPage
                        var summaryScope = RenderFormModel.BuildHistoryScope(ReferenceNumber, TaskId, CurrentPageId);
                        _navigationHistoryService.Clear(summaryScope);

                        var summaryUrl = _formNavigationService.GetTaskSummaryUrl(CurrentTask.TaskId, ReferenceNumber);

                        return Redirect(summaryUrl);
                    }
                    
                    // returnToSummaryPage=false - proceed with normal next page logic
                    string? nextPageId = null;
                    
                    if (ConditionalState != null && Template != null)
                    {
                        _logger.LogInformation("[FLOW DEBUG] ReturnToSummaryPage=false path - currentPageId: {PageId}", CurrentPage.PageId);
                        _logger.LogInformation("[FLOW DEBUG] Data before calling GetNextPageAsync:");
                        foreach (var kv in Data.Take(10))
                        {
                            _logger.LogInformation("[FLOW DEBUG] Data[{Key}] = {Value}", kv.Key, kv.Value?.ToString() ?? "null");
                        }

                        var context = new ConditionalLogicContext
                        {
                            CurrentPageId = CurrentPageId,
                            CurrentTaskId = TaskId,
                            IsClientSide = false,
                            Trigger = "change"
                        };
                        
                        nextPageId = await _conditionalLogicOrchestrator.GetNextPageAsync(Template, Data, CurrentPage.PageId, context);
                        _logger.LogInformation("[FLOW DEBUG] GetNextPageAsync returned: {NextPageId}", nextPageId ?? "null");
                    }
                    
                    // If conditional logic found a next page, navigate to it
                    if (!string.IsNullOrEmpty(nextPageId))
                    {
                        var nextUrl = $"/applications/{ReferenceNumber}/{CurrentTask.TaskId}/{nextPageId}";

                        return Redirect(nextUrl);
                    }
                    
                    // No conditional next page - find the next page in sequence
                    var sequentialNextPage = FormStepPolicy.GetNextPage(CurrentTask.Pages, CurrentPage.PageId);
                    
                    if (sequentialNextPage != null)
                    {
                        var nextUrl = $"/applications/{ReferenceNumber}/{CurrentTask.TaskId}/{sequentialNextPage.PageId}";

                        return Redirect(nextUrl);
                    }

                    // No next page found - go to task summary as fallback
                    var summaryFallbackScope = RenderFormModel.BuildHistoryScope(ReferenceNumber, TaskId, CurrentPageId);
                    _navigationHistoryService.Clear(summaryFallbackScope);

                    var fallbackUrl = _formNavigationService.GetTaskSummaryUrl(CurrentTask.TaskId, ReferenceNumber);

                    return Redirect(fallbackUrl);
                }
            }
            else if (CurrentTask != null)
            {
                // Fallback: redirect to the appropriate summary/list depending on config
                if (_formStateManager.ShouldShowCollectionFlowSummary(CurrentTask))
                {
                    var url = _formNavigationService.GetCollectionFlowSummaryUrl(CurrentTask.TaskId, ReferenceNumber);
                    return Redirect(url);
                }
                if (_formStateManager.ShouldShowDerivedCollectionFlowSummary(CurrentTask))
                {
                    var completedValue = Request.Form["IsTaskCompleted"].ToString();
                    var isCompleted = !string.IsNullOrEmpty(completedValue) &&
                        (string.Equals(completedValue, "true", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(completedValue, "on", StringComparison.OrdinalIgnoreCase));
                    
                    if (isCompleted)
                    {
                        var derivedFlows = CurrentTask?.Summary?.DerivedFlows;
                        var errorLines = new List<string>();
                        
                        if (derivedFlows != null && derivedFlows.Any())
                        {
                            foreach (var derivedFlow in derivedFlows)
                            {
                                var derivedItems = _derivedCollectionFlowService.GenerateItemsFromSourceField(
                                    derivedFlow.SourceFieldId, FormData, derivedFlow);
                                
                                if (!derivedItems.Any())
                                {
                                    // Use template-defined error message or fallback to default
                                    var errorMessage = !string.IsNullOrEmpty(derivedFlow.NoItemsErrorMessage)
                                        ? derivedFlow.NoItemsErrorMessage
                                        : $"You need to add at least one item before signing the {derivedFlow.Title}";
                                    errorLines.Add(errorMessage);
                                    continue;
                                }
                                
                                var statuses = _derivedCollectionFlowService.GetItemStatuses(derivedFlow.FieldId, FormData);
                                
                                var unsignedItems = derivedItems
                                    .Where(item => !statuses.ContainsKey(item.Id) || statuses[item.Id] != "Signed")
                                    .ToList();
                                
                                if (unsignedItems.Any())
                                {
                                    foreach (var item in unsignedItems)
                                    {
                                        var displayName = GetDerivedItemDisplayName(derivedFlow, item.Id);
                                        // Use template-defined error message or fallback to default
                                        var errorMessage = !string.IsNullOrEmpty(derivedFlow.UnsignedItemErrorMessage)
                                            ? derivedFlow.UnsignedItemErrorMessage.Replace("{sourceName}", displayName)
                                            : $"You need to sign the declaration for {displayName}";
                                        errorLines.Add(errorMessage);
                                    }
                                }
                            }
                        }
                        
                        if (errorLines.Any())
                        {
                            ModelState.Clear();
                            // Add header message
                            ModelState.AddModelError("", "You cannot mark this section as complete:");
                            // Add each error as a separate ModelState entry so they render as bullet points
                            foreach (var errorLine in errorLines)
                            {
                                ModelState.AddModelError("", errorLine);
                            }
                            IsTaskCompleted = false;
                            
                            //  Ensure CurrentFormState is set correctly for the view to render properly
                            CurrentFormState = FormState.DerivedCollectionFlowSummary;
                            
                            //  Load FormData from session so the view can render the derived flow sections
                            LoadFormDataFromSession();
                            
                            return Page();
                        }
                    }

                    if (ApplicationId.HasValue)
                    {
                        if (isCompleted)
                        {
                            await _applicationStateService.SaveTaskStatusAsync(
                                ApplicationId.Value,
                                CurrentTask.TaskId,
                                Domain.Models.TaskStatus.Completed);
                        }
                        else
                        {
                            var hasAnyData = _applicationStateService.CalculateTaskStatus(CurrentTask.TaskId, Template, FormData, ApplicationId, ApplicationStatus)
                                != Domain.Models.TaskStatus.NotStarted;
                            var newStatus = hasAnyData ? Domain.Models.TaskStatus.InProgress : Domain.Models.TaskStatus.NotStarted;
                            await _applicationStateService.SaveTaskStatusAsync(
                                ApplicationId.Value,
                                CurrentTask.TaskId,
                                newStatus);
                        }
                    }

                    var taskListUrl = _formNavigationService.GetTaskListUrl(ReferenceNumber);
                    
                    return Redirect(taskListUrl);
                }
                var summaryUrl = $"/applications/{ReferenceNumber}/{CurrentTask.TaskId}";
                return Redirect(summaryUrl);
            }
            // Fallback: redirect to task list if CurrentTask is null
            var listUrl = $"/applications/{ReferenceNumber}";
            return Redirect(listUrl);
        }

        public async Task<IActionResult> OnGetAutocompleteAsync(string endpoint, string query)
        {
            

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                
                return new JsonResult(new List<object>());
            }

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

        // Removed: superseded by RemoveFieldItem page handler

        public async Task<IActionResult> OnPostRemoveCollectionItemAsync(string fieldId, string itemId, string? flowId = null)
        {
            await CommonFormEngineInitializationAsync();
            
            ModelState.Clear();
            
            if (!string.IsNullOrEmpty(TaskId))
            {
                var (group, task) = InitializeCurrentTask(TaskId);
                CurrentGroup = group;
                CurrentTask = task;
            }
            
            if (string.IsNullOrEmpty(fieldId) || string.IsNullOrEmpty(itemId))
            {
                return BadRequest("Field ID and Item ID are required");
            }

            if (!IsApplicationEditable())
            {
                ModelState.AddModelError("", ApplicationAccessMessages.NoWritePermission);
                return Page();
            }

            bool isConfirmed = Request.Query.ContainsKey("confirmed") && Request.Query["confirmed"] == "true";
            
            if (!isConfirmed)
            {
                _logger.LogInformation("RemoveCollectionItem handler executing for validation - item will not be removed yet");
                
                return Redirect(_formNavigationService.GetCollectionFlowSummaryUrl(TaskId, ReferenceNumber));
            }
            
            _logger.LogInformation("RemoveCollectionItem handler executing confirmed removal for item {ItemId} from field {FieldId}", itemId, fieldId);

            // Get current collection from session first
            var accumulatedData = _applicationResponseService.GetAccumulatedFormData();
            
            Dictionary<string, object>? itemData = null;
            string? flowTitle = null;
            
            // Get the flow and item information for success message
            if (!string.IsNullOrEmpty(flowId) && CurrentTask != null)
            {
                var flow = CurrentTask.Summary?.Flows?.FirstOrDefault(f => f.FlowId == flowId);
                if (flow != null)
                {
                    flowTitle = flow.Title;
                    
                    // Get the item data before removing it
                    if (accumulatedData.TryGetValue(fieldId, out var collectionValue))
                    {
                        var json = collectionValue?.ToString() ?? "[]";
                        try
                        {
                            var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json) ?? new();
                            itemData = items.FirstOrDefault(i => i.TryGetValue("id", out var id) && id?.ToString() == itemId);
                        }
                        catch { }
                    }
                    
                    // Generate success message using custom message or fallback
                    itemData = ExpandEncodedJson(itemData);
                    SuccessMessage = GenerateSuccessMessage(flow.DeleteItemMessage, "delete", itemData, flowTitle);
                }
            }

            // Now perform the actual removal
            if (accumulatedData.TryGetValue(fieldId, out var collectionData))
            {
                var json = collectionData?.ToString() ?? "[]";
                try
                {
                    var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json) ?? new();
                    
                    // Find the item to be removed so we can delete its associated files
                    var itemToRemove = items.FirstOrDefault(item => 
                        item.TryGetValue("id", out var id) && id?.ToString() == itemId);
                    
                    // Delete all files associated with this collection item before removing it
                    if (itemToRemove != null && ApplicationId.HasValue)
                    {
                        // Expand any encoded JSON in the item data to ensure file data is properly parsed
                        var expandedItem = ExpandEncodedJson(itemToRemove);
                        await DeleteFilesFromCollectionItemAsync(ApplicationId.Value, expandedItem);
                    }
                    
                    // Remove the item with matching ID
                    items.RemoveAll(item => item.TryGetValue("id", out var id) && id?.ToString() == itemId);
                    
                    // Update the collection
                    var updatedJson = JsonSerializer.Serialize(items);
                    _applicationResponseService.AccumulateFormData(new Dictionary<string, object> { [fieldId] = updatedJson });
                    
                    // Save to API
                    if (ApplicationId.HasValue)
                    {
                        await _applicationResponseService.SaveApplicationResponseAsync(ApplicationId.Value, new Dictionary<string, object> { [fieldId] = updatedJson });
                    }
                }
                catch (ExternalApplicationsException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove collection item {ItemId} from field {FieldId}", itemId, fieldId);
                }
            }

            // Redirect back to the collection summary
            return Redirect(_formNavigationService.GetCollectionFlowSummaryUrl(TaskId, ReferenceNumber));
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



        private static bool TryParseFlowRoute(string pageId, out string flowId, out string instanceId, out string flowPageId)
        {
            if (FormRouteParser.TryParseCollectionFlow(pageId, out var route))
            {
                flowId = route.FlowId;
                instanceId = route.InstanceId;
                flowPageId = route.PageId;
                return true;
            }

            flowId = instanceId = flowPageId = string.Empty;
            return false;
        }

        private static bool TryParseDerivedFlowRoute(string pageId, out string derivedFlowId, out string derivedItemId, out string derivedPageId)
        {
            if (FormRouteParser.TryParseDerivedFlow(pageId, out var route))
            {
                derivedFlowId = route.FlowId;
                derivedItemId = route.ItemId;
                derivedPageId = route.PageId;
                return true;
            }

            derivedFlowId = derivedItemId = derivedPageId = string.Empty;
            return false;
        }

        private static List<Domain.Models.Page>? GetFlowPages(Domain.Models.Task? task, string flowId) =>
            FormStepPolicy.GetCollectionFlowPages(task, flowId)?.ToList();

        private static string? GetFlowFieldId(Domain.Models.Task? task, string flowId) =>
            FormStepPolicy.GetCollectionFlowFieldId(task, flowId);

        private static DerivedCollectionFlowConfiguration? GetDerivedFlowConfiguration(Domain.Models.Task? task, string derivedFlowId) =>
            FormStepPolicy.GetDerivedFlow(task, derivedFlowId);

        /// <summary>
        /// Loads pre-filled data for a derived collection item
        /// </summary>
        private void LoadDerivedItemData(DerivedCollectionFlowConfiguration config, string itemId)
        {
            try
            {
                // First, load any existing declaration data for this item
                var existingData = _derivedCollectionFlowService.GetItemDeclarationData(config.FieldId, itemId, FormData);
                foreach (var kvp in existingData)
                {
                    Data[kvp.Key] = kvp.Value;
                }

                // Then, generate and load pre-filled data from the source
                var derivedItems = _derivedCollectionFlowService.GenerateItemsFromSourceField(config.SourceFieldId, FormData, config);
                var currentItem = derivedItems.FirstOrDefault(item => item.Id == itemId);
                
                if (currentItem != null)
                {
                    // Pre-fill with source data (but don't overwrite existing declaration data)
                    foreach (var kvp in currentItem.PrefilledData)
                    {
                        if (!Data.ContainsKey(kvp.Key)) // Only set if not already populated from existing data
                        {
                            Data[kvp.Key] = kvp.Value;
                        }
                    }
                    
                    _logger.LogInformation("Loaded derived item data for item {ItemId} in flow {FlowId} with {Count} fields", 
                        itemId, config.FlowId, currentItem.PrefilledData.Count);
                }
                
                // Ensure all field labels are visible for derived flow forms
                if (CurrentPage != null)
                {
                    foreach (var field in CurrentPage.Fields)
                    {
                        if (field.Label != null)
                        {
                            field.Label.IsVisible = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load derived item data for item {ItemId} in flow {FlowId}", itemId, config.FlowId);
            }
        }

        /// <summary>
        /// Resolves a user-friendly display name for a derived item, using the service's generated
        /// items and the configured binding. Falls back to the raw itemId if no data is available.
        /// </summary>
        private string GetDerivedItemDisplayName(DerivedCollectionFlowConfiguration config, string itemId)
        {
            try
            {
                var items = _derivedCollectionFlowService.GenerateItemsFromSourceField(config.SourceFieldId, FormData, config);
                var match = items.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    if (!string.IsNullOrWhiteSpace(match.DisplayName))
                    {
                        return match.DisplayName;
                    }

                    if (match.PrefilledData != null &&
                        match.PrefilledData.TryGetValue(config.ItemTitleBinding, out var value) &&
                        !string.IsNullOrWhiteSpace(value?.ToString()))
                    {
                        return value!.ToString()!;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return itemId;
        }

        /// <summary>
        /// Checks if an item with the given instanceId already exists in the collection
        /// </summary>
        private bool IsExistingCollectionItem(string fieldId, string instanceId)
        {
            var accumulated = _applicationResponseService.GetAccumulatedFormData();
            if (accumulated.TryGetValue(fieldId, out var collectionValue))
            {
                var json = collectionValue?.ToString() ?? "[]";
                try
                {
                    var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json) ?? new();
                    return items.Any(item => item.TryGetValue("id", out var id) && id?.ToString() == instanceId);
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Reads a collection field value from FormData and parses it to a list of item dictionaries.
        /// Returns an empty list when missing or invalid.
        /// </summary>
        private List<Dictionary<string, object>> ReadCollectionItemsFromFormData(string fieldId)
        {
            if (!FormData.TryGetValue(fieldId, out var value) || value == null)
            {
                return new List<Dictionary<string, object>>();
            }
            var s = value.ToString();
            if (string.IsNullOrWhiteSpace(s) || !s!.TrimStart().StartsWith("["))
            {
                return new List<Dictionary<string, object>>();
            }
            try
            {
                var parsed = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(s);
                return parsed ?? new List<Dictionary<string, object>>();
            }
            catch
            {
                return new List<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Checks if a task has any data (for regular tasks or collection flows)
        /// </summary>
        private bool HasAnyTaskData(Domain.Models.Task task)
        {
            var taskFieldIds = new List<string>();
            
            // For regular tasks, get field IDs from pages
            if (task.Pages != null)
            {
                taskFieldIds.AddRange(task.Pages
                    .SelectMany(p => p.Fields)
                    .Select(f => f.FieldId));
            }
            
            // For multi-collection flow tasks, also check collection field IDs
            if (task.Summary?.Mode?.Equals("multiCollectionFlow", StringComparison.OrdinalIgnoreCase) == true &&
                task.Summary.Flows != null)
            {
                taskFieldIds.AddRange(task.Summary.Flows.Select(f => f.FieldId));
            }
                
            return taskFieldIds.Any(fieldId => 
                FormData.ContainsKey(fieldId) && 
                !string.IsNullOrWhiteSpace(FormData[fieldId]?.ToString()));
        }

        private void AppendCollectionItemToSession(List<Domain.Models.Page> pages, string fieldId, string instanceId, Dictionary<string, object> itemData)
        {
            var acc = _applicationResponseService.GetAccumulatedFormData();
            var list = new List<Dictionary<string, object>>();
            if (acc.TryGetValue(fieldId, out var existing))
            {
                var s = existing?.ToString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(s);
                        if (parsed != null) list = parsed;
                    }
                    catch { }
                }
            }



            // Find existing item or create new one
            var idx = list.FindIndex(x => x.TryGetValue("id", out var id) && id?.ToString() == instanceId);
            Dictionary<string, object> item;
            
            if (idx >= 0)
            {
                // Editing existing item: start with existing data and merge in new values
                item = new Dictionary<string, object>(list[idx]);
                
                // Update only the fields that have values in itemData (current page data)
                foreach (var kvp in itemData)
                {
                    // If the incoming value is the upload placeholder, do not overwrite an existing upload JSON
                    if (kvp.Value?.ToString() == "UPLOAD_FIELD_SESSION_DATA" &&
                        item.TryGetValue(kvp.Key, out var existingVal) &&
                        existingVal != null && existingVal.ToString()!.StartsWith("[") && existingVal.ToString()!.Contains("\"id\""))
                    {
                        continue;
                    }
                    item[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                // New item: create fresh item with all possible fields from flow pages
                item = new Dictionary<string, object>();
                foreach (var page in pages)
                {
                    foreach (var field in page.Fields)
                    {
                        var key = field.FieldId;
                        if (itemData.TryGetValue(key, out var value))
                        {
                            // Skip placeholder writes for uploads; real value will be in itemData when available
                            if (value?.ToString() == "UPLOAD_FIELD_SESSION_DATA")
                            {
                                continue;
                            }
                            item[key] = value;
                        }
                    }
                }
                item["id"] = instanceId;
            }

            // Ensure id is always set
            item["id"] = instanceId;

            // DEBUG: Log final item before serialization

            foreach (var kvp in item)
            {
                var valueStr = kvp.Value?.ToString();
                var preview = valueStr?.Length > 100 ? valueStr.Substring(0, 100) + "..." : valueStr;

                if (kvp.Key.Contains("upload", StringComparison.OrdinalIgnoreCase))
                {

                }
            }

            // Upsert the item
            if (idx >= 0) 
                list[idx] = item; 
            else 
                list.Add(item);

            var serialized = JsonSerializer.Serialize(list);

            _applicationResponseService.AccumulateFormData(new Dictionary<string, object> { [fieldId] = serialized });
        }

        private FormFileFieldContext FileFieldContext => new(ApplicationId, FlowId, InstanceId);

        private void CheckAndClearSessionForNewApplication()
        {
            // Check if we're working with a different application than what's stored in session
            var sessionApplicationId = HttpContext.Session.GetString(FormSessionKeys.CurrentAccumulatedApplicationId);
            var currentApplicationId = ApplicationId?.ToString();

            if (!string.IsNullOrEmpty(sessionApplicationId) &&
                sessionApplicationId != currentApplicationId)
            {
                // Clear accumulated data for the previous application
                _applicationResponseService.ClearAccumulatedFormData();
                _logger.LogInformation("Cleared accumulated form data for previous application {PreviousApplicationId}, now working with {CurrentApplicationId}",
                    sessionApplicationId, currentApplicationId);
            }

            // Store the current application ID for future reference
            if (ApplicationId.HasValue)
            {
                HttpContext.Session.SetString("CurrentAccumulatedApplicationId", ApplicationId.Value.ToString());
            }
        }

        private async Task LoadAccumulatedDataFromSessionAsync()
        {
            // Get accumulated form data from session and populate the Data dictionary
            // Infected files are automatically filtered by the blacklist
            var accumulatedData = _applicationResponseService.GetAccumulatedFormData();

            if (accumulatedData.Any())
            {
                // Populate the Data dictionary with accumulated data
                foreach (var kvp in accumulatedData)
                {
                    Data[kvp.Key] = kvp.Value;
                }

                _logger.LogInformation("Loaded {Count} accumulated form data entries from session", accumulatedData.Count);
            }

            // Apply conditional logic after loading data
            await ApplyConditionalLogicAsync();
        }

                private async Task ApplyConditionalLogicAsync(string trigger = "load")
                    {
                        try
                        {
                

                if (Template?.ConditionalLogic != null && Template.ConditionalLogic.Any())
                {
                    // Prefer FormData (session) when Data is empty (e.g. preview GET before bind);
                    // otherwise use Data so current-page POST values win.
                    var dataForConditionalLogic = Data.Count > 0
                        ? new Dictionary<string, object>(Data)
                        : new Dictionary<string, object>(FormData);
                    
                    // Only merge when in POST/change trigger (not during initial GET/load)
                    if (trigger == "change")
                    {
                        var accumulatedData = _applicationResponseService.GetAccumulatedFormData();
                        foreach (var kvp in accumulatedData)
                        {
                            // Only add if not already in dataForConditionalLogic (current page data takes priority)
                            if (!dataForConditionalLogic.ContainsKey(kvp.Key))
                            {
                                dataForConditionalLogic[kvp.Key] = kvp.Value;
                            }
                        }
                    }

                    var context = new ConditionalLogicContext
                    {
                        CurrentPageId = CurrentPageId,
                        CurrentTaskId = TaskId,
                        IsClientSide = false,
                        Trigger = trigger
                    };

                    ConditionalState = await _conditionalLogicOrchestrator.ApplyConditionalLogicAsync(Template, dataForConditionalLogic, context);
                    
                    
                    
                    // Apply field values from conditional logic
                    if (ConditionalState.FieldValues.Any())
                    {
                        foreach (var kvp in ConditionalState.FieldValues)
                        {
                            Data[kvp.Key] = kvp.Value;
                        }
                        
                    }
                }
                else
                {
                    
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CONDITIONAL LOGIC ERROR: {Message}", ex.Message);
            }
        }



        /// <summary>
        /// Calculate overall application status based on task statuses
        /// </summary>
        public string CalculateApplicationStatus()
        {
            if (Template?.TaskGroups == null)
            {
                return "InProgress";
            }

            var allTasks = Template.TaskGroups.SelectMany(g => g.Tasks).ToList();

            // If any task is in progress or completed, application is in progress
            var hasAnyTaskWithProgress = allTasks.Any(task =>
            {
                var status = GetTaskStatusFromSession(task.TaskId);
                return status == Domain.Models.TaskStatus.InProgress || status == Domain.Models.TaskStatus.Completed;
            });

            return hasAnyTaskWithProgress ? "InProgress" : "InProgress"; // Always InProgress until submitted
        }

        private void LoadExistingFlowItemData(string flowId, string instanceId)
        {
            // Check if we're editing an existing item by looking in the collection
            var task = CurrentTask;
            var fieldId = GetFlowFieldId(task, flowId);
            
            if (string.IsNullOrEmpty(fieldId)) return;

            var accumulated = _applicationResponseService.GetAccumulatedFormData();
            if (accumulated.TryGetValue(fieldId, out var collectionValue))
            {
                var json = collectionValue?.ToString() ?? "[]";
                try
                {
                    var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json) ?? new();
                    var existingItem = items.FirstOrDefault(item => item.TryGetValue("id", out var id) && id?.ToString() == instanceId);
                    
                    if (existingItem != null)
                    {
                        // Editing existing item: load its data into Data dictionary for form rendering
                        foreach (var kvp in existingItem)
                        {
                            if (kvp.Key == "id") continue; // Skip the ID field
                            // Preserve upload data if present in saved item
                            if (kvp.Value != null && kvp.Value.ToString()?.StartsWith("[") == true && kvp.Value.ToString()!.Contains("\"id\""))
                            {
                                Data[kvp.Key] = kvp.Value;
                                continue;
                            }
                            Data[kvp.Key] = kvp.Value;
                        }

                    }
                    else
                    {
                        // New item: check if this is the first page or if we have progress
                        var existingProgress = _collectionFlowProgressStore.Load(flowId, instanceId);
                        if (existingProgress.Any())
                        {
                            // We have progress, this is not the first page - load the progress
                            foreach (var kvp in existingProgress)
                            {
                                Data[kvp.Key] = kvp.Value;
                            }

                        }
                        else
                        {
                            // No progress exists, this is likely the first page - ensure clean start
                            _collectionFlowProgressStore.Clear(flowId, instanceId);
                            Data.Clear();

                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load existing flow item data for instance {InstanceId}", instanceId);
                }
            }
            else
            {
                // No collection exists yet - check for existing progress
                var existingProgress = _collectionFlowProgressStore.Load(flowId, instanceId);
                if (existingProgress.Any())
                {
                    // Load existing progress
                    foreach (var kvp in existingProgress)
                    {
                        Data[kvp.Key] = kvp.Value;
                    }

                }
                else
                {
                    // Truly new - clear everything
                    _collectionFlowProgressStore.Clear(flowId, instanceId);
                    Data.Clear();

                }
            }
        }

        /// <summary>
        /// Check if a field should be hidden based on conditional logic
        /// </summary>
        /// <param name="fieldId">The field ID to check</param>
        /// <returns>True if the field should be hidden</returns>
        public bool IsFieldHidden(string fieldId)
        {
            if (ConditionalState == null)
            {
                // If no conditional state but field has conditional logic rules, hide it by default
                if (Template?.ConditionalLogic != null && HasFieldConditionalLogic(fieldId))
                {
                    return true;
                }
                return false;
            }

            if (ConditionalState.FieldVisibility.TryGetValue(fieldId, out var isVisible))
            {
                return !isVisible;
            }
            
            // Check if field has conditional logic rules - if so, hide by default until conditions are met
            if (Template?.ConditionalLogic != null && HasFieldConditionalLogic(fieldId))
            {
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Check if a field has conditional logic rules that affect its visibility
        /// </summary>
        /// <param name="fieldId">The field ID to check</param>
        /// <returns>True if the field has conditional visibility rules</returns>
        private bool HasFieldConditionalLogic(string fieldId)
        {
            if (Template?.ConditionalLogic == null) return false;

            _fieldsWithConditionalVisibility ??= BuildFieldsWithConditionalVisibility();
            return _fieldsWithConditionalVisibility.Contains(fieldId);
        }

        private HashSet<string> BuildFieldsWithConditionalVisibility()
        {
            var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Template?.ConditionalLogic == null)
                return fields;

            foreach (var rule in Template.ConditionalLogic)
            {
                if (!rule.Enabled || rule.AffectedElements == null)
                    continue;

                foreach (var element in rule.AffectedElements)
                {
                    if (element.ElementType == "field"
                        && (element.Action == "hide" || element.Action == "show")
                        && !string.IsNullOrEmpty(element.ElementId))
                    {
                        fields.Add(element.ElementId);
                    }
                }
            }

            return fields;
        }

        /// <summary>
        /// Check if a page should be hidden/skipped based on conditional logic
        /// </summary>
        /// <param name="pageId">The page ID to check</param>
        /// <returns>True if the page should be hidden</returns>
        public bool IsPageHidden(string pageId)
        {

            
            if (ConditionalState == null)
            {

                // If no conditional state but page has conditional logic rules, hide it by default
                if (Template?.ConditionalLogic != null && HasPageConditionalLogic(pageId))
                {

                    return true;
                }
                return false;
            }

            // Check if page is in skipped list
            if (ConditionalState.SkippedPages.Contains(pageId))
            {

                return true;
            }

            // Check if page is hidden by visibility rules
            if (ConditionalState.PageVisibility.TryGetValue(pageId, out var isVisible))
            {
                // Trust the ConditionalState that was already calculated by ApplyConditionalLogicAsync
                return !isVisible;
            }
            
            // If page is not in ConditionalState.PageVisibility but has conditional logic rules, hide it by default
            if (Template?.ConditionalLogic != null && HasPageConditionalLogic(pageId))
            {
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Check if a page has conditional logic rules that affect its visibility
        /// </summary>
        /// <param name="pageId">The page ID to check</param>
        /// <returns>True if the page has conditional visibility rules</returns>
        private bool HasPageConditionalLogic(string pageId)
        {
            if (Template?.ConditionalLogic == null) return false;
            
            return Template.ConditionalLogic.Any(rule => 
                rule.Enabled && 
                rule.AffectedElements.Any(element => 
                    element.ElementId == pageId && 
                    element.ElementType == "page" && 
                    (element.Action == "hide" || element.Action == "show" || element.Action == "skip")));
        }

        /// <summary>
        /// Check if conditional logic was actually triggered based on current data and field changes
        /// </summary>
        /// <returns>True if any conditional logic rules were triggered</returns>
        private bool HasConditionalLogicTriggered()
        {
            if (Template?.ConditionalLogic == null || ConditionalState == null)
            {
                return false;
            }

            // Check if any rules have their conditions met with current data
            foreach (var rule in Template.ConditionalLogic.Where(r => r.Enabled))
            {
                if (EvaluateRuleConditions(rule))
                {

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if conditional logic specifically shows/reveals new pages based on current form data
        /// </summary>
        /// <returns>True if conditional logic rules with "show" actions are met by current data</returns>
        private bool HasConditionalLogicShowingPages()
        {
            if (Template?.ConditionalLogic == null)
                return false;
            
            foreach (var rule in Template.ConditionalLogic.Where(r => r.Enabled))
            {
                // Only check rules that have "show" actions for pages
                var hasShowPageAction = rule.AffectedElements.Any(element => 
                    element.ElementType == "page" && element.Action == "show");
                
                if (!hasShowPageAction) continue;
                

                
                if (EvaluateRuleConditions(rule))
                {

                    return true;
                }
            }
            

            return false;
        }

        /// <summary>
        /// Evaluate if a conditional logic rule's conditions are met
        /// </summary>
        /// <param name="rule">The rule to evaluate</param>
        /// <returns>True if all conditions are met</returns>
        private bool EvaluateRuleConditions(Domain.Models.ConditionalLogic rule)
        {
            if (rule.ConditionGroup?.Conditions == null || !rule.ConditionGroup.Conditions.Any())
            {
                return false;
            }

            var results = new List<bool>();
            
            foreach (var condition in rule.ConditionGroup.Conditions)
            {
                var fieldValue = Data.TryGetValue(condition.TriggerField, out var value) ? value?.ToString() : "";
                var conditionValue = condition.Value?.ToString() ?? "";
                var conditionMet = condition.Operator.ToLower() switch
                {
                    "equals" => string.Equals(fieldValue, conditionValue, StringComparison.OrdinalIgnoreCase),
                    "not_equals" => !string.Equals(fieldValue, conditionValue, StringComparison.OrdinalIgnoreCase),
                    "contains" => fieldValue?.Contains(conditionValue, StringComparison.OrdinalIgnoreCase) == true,
                    "not_contains" => fieldValue?.Contains(conditionValue, StringComparison.OrdinalIgnoreCase) != true,
                    _ => false
                };
                
                results.Add(conditionMet);
            }

            // Apply logical operator
            return rule.ConditionGroup.LogicalOperator?.ToUpper() switch
            {
                "AND" => results.All(r => r),
                "OR" => results.Any(r => r),
                _ => results.All(r => r) // Default to AND
            };
        }

        /// <summary>
        /// Ensures field visibility for a collection item is evaluated for the given field IDs (batched).
        /// Call once per item before checking multiple summary columns.
        /// </summary>
        public void EnsureItemFieldVisibility(Dictionary<string, object> itemData, IEnumerable<string> fieldIds)
        {
            if (Template?.ConditionalLogic == null || !Template.ConditionalLogic.Any())
                return;

            var needed = fieldIds
                .Where(HasFieldConditionalLogic)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(id =>
                    !_itemConditionalStateCache.TryGetValue(itemData, out var existing)
                    || !existing.FieldVisibility.ContainsKey(id))
                .ToList();

            if (needed.Count == 0)
                return;

            try
            {
                var context = new ConditionalLogicContext
                {
                    CurrentPageId = CurrentPageId,
                    CurrentTaskId = TaskId,
                    IsClientSide = false,
                    Trigger = "load"
                };

                var partial = _conditionalLogicOrchestrator
                    .ApplyFieldVisibilityAsync(Template, itemData, needed, context)
                    .GetAwaiter()
                    .GetResult();

                if (!_itemConditionalStateCache.TryGetValue(itemData, out var state))
                {
                    _itemConditionalStateCache[itemData] = partial;
                    return;
                }

                foreach (var kvp in partial.FieldVisibility)
                {
                    state.FieldVisibility[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring field visibility for collection item");
            }
        }

        /// <summary>
        /// Check if a field should be hidden for a specific collection item based on conditional logic
        /// </summary>
        /// <param name="fieldId">The field ID to check</param>
        /// <param name="itemData">The specific item's data to evaluate against</param>
        /// <returns>True if the field should be hidden for this specific item</returns>
        public bool IsFieldHiddenForItem(string fieldId, Dictionary<string, object> itemData)
        {
            try
            {
                if (Template?.ConditionalLogic == null || !Template.ConditionalLogic.Any())
                {
                    return false; // No conditional logic defined
                }

                if (!HasFieldConditionalLogic(fieldId))
                {
                    return false;
                }

                EnsureItemFieldVisibility(itemData, [fieldId]);

                if (_itemConditionalStateCache.TryGetValue(itemData, out var itemConditionalState)
                    && itemConditionalState.FieldVisibility.TryGetValue(fieldId, out var isVisible))
                {
                    return !isVisible;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating conditional logic for field {FieldId} with item data", fieldId);
                return false; // Default to visible on error
            }
        }

        #region Upload File Handlers

        public async Task<IActionResult> OnPostUploadFileAsync()
        {
            
            // Ensure Template is not null (required for RenderForm)
            if (Template == null)
            {
                Template = new FormTemplate
                {
                    TemplateId = "dummy",
                    TemplateName = "dummy",
                    Description = "dummy",
                    TaskGroups = new List<TaskGroup>()
                };
            }
            
            // Align POST context with GET so CurrentTask/Data are available
            try
            {
                await CommonFormEngineInitializationAsync();

            }
            catch (Exception ex)
            {

            }
            
            // Extract form data
            var applicationId = Request.Form["ApplicationId"].ToString();
            var fieldId = Request.Form["FieldId"].ToString();
            var returnUrl = Request.Form["ReturnUrl"].ToString();
            var uploadDescription = Request.Form["UploadDescription"].ToString();
            
            // Clear validation errors for FlowId/InstanceId if not in collection flow
            if (!IsCollectionFlow)
            {
                ModelState.Remove("FlowId");
                ModelState.Remove("InstanceId");
            }
            
            // Parse application ID
            if (!Guid.TryParse(applicationId, out var appId))
            {
                return NotFound();
            }
            
            // Get uploaded file
            var file = Request.Form.Files["UploadFile"];
            // Read any existing file IDs posted by the view to preserve list
            var existingFileIds = Request.Form["ExistingFileIds"].ToArray();
            
            if (file == null || file.Length == 0)
            {

                ErrorMessage = "Select a file to upload";
                ModelState.AddModelError("UploadFile", ErrorMessage);

                if (!string.IsNullOrEmpty(fieldId))
                {
                    _formErrorStore.Save(fieldId, ModelState);
                }

                Files = _formFileFieldService.GetFiles(new FormFileFieldContext(appId, FlowId, InstanceId), fieldId);
                
                // Check if we have return URL
                if (!string.IsNullOrEmpty(returnUrl))
                {

                    return Redirect(returnUrl);
                }
                
                return Page();
            }

            if (_formFileFieldService.ContainsFileName(new FormFileFieldContext(appId, FlowId, InstanceId), fieldId, file.FileName))
            {
                ErrorMessage = "The selected file has already been uploaded. Upload a file with a different name.\n ";
                ModelState.AddModelError("UploadFile", ErrorMessage);

                if (!string.IsNullOrEmpty(fieldId))
                {
                    _formErrorStore.Save(fieldId, ModelState);
                }

                Files = _formFileFieldService.GetFiles(new FormFileFieldContext(appId, FlowId, InstanceId), fieldId);

                if (!string.IsNullOrEmpty(returnUrl))
                {

                    return Redirect(returnUrl);
                }

                return Page();
            }

            using var stream = file.OpenReadStream();
            var fileParam = new FileParameter(stream, file.FileName, file.ContentType);
            
            try
            {
                var uploadedFile = await fileUploadService.UploadFileAsync(appId, file.FileName, uploadDescription, fileParam);

                
                // Only execute this code if API call succeeds
                // Get existing files for this field/collection instance
                var currentFieldFiles = _formFileFieldService.GetFiles(new FormFileFieldContext(appId, FlowId, InstanceId), fieldId).ToList();
                
                if (!currentFieldFiles.Any(cf => cf.Id == uploadedFile.Id))
                {
                    _logger.LogInformation(
                        "Adding newly uploaded file {FileId} ({FileName}) to field {FieldId}",
                        uploadedFile.Id,
                        uploadedFile.OriginalFileName,
                        fieldId);
                    currentFieldFiles.Add(uploadedFile);
                }
                
                //  Filter infected files AFTER adding the newly uploaded file
                // This ensures the file appears briefly, then gets removed by the consumer
                currentFieldFiles = FilterInfectedFilesFromList(currentFieldFiles);
                
                _formFileFieldService.SaveFiles(new FormFileFieldContext(appId, FlowId, InstanceId), fieldId, currentFieldFiles);
                //  Do NOT save to database on upload! Files are saved when user clicks "Continue"
                // This gives the virus scanner time to process and blacklist infected files
                
                // 1. Field-level key (used by the view partial)
                _formErrorStore.Clear(fieldId);
                // 2. Page-level context key (used by validation in OnPostPageAsync) - use same method to ensure exact match
                var pageContextKey = GetFormErrorContextKey();
                _formErrorStore.Clear(pageContextKey);
                // 3. Clear any errors already loaded into ModelState for this field
                ModelState.Remove(fieldId);
                ModelState.Remove($"Data[{fieldId}]");
                _logger.LogInformation("Cleared FormErrorStore (fieldKey: {FieldId}, contextKey: {PageContext}) and ModelState after successful upload", 
                    fieldId, pageContextKey);
                
                // Set success message
                SuccessMessage = $"Your file '{file.FileName}' uploaded.";

                
                // Send notification
                var addRequest = new AddNotificationRequest
                {
                    Message = SuccessMessage,
                    Category = "file-upload",
                    Context = $"file-upload|{uploadedFile.Id}",
                    Type = NotificationType.Success,
                    AutoDismiss = false,
                    AutoDismissSeconds = 5,
                    ReplaceExistingContext = false
                };
                await TryCreateFileNotificationAsync(addRequest);

                
                // Redirect back if we have return URL
                if (!string.IsNullOrEmpty(returnUrl))
                {

                    return Redirect(returnUrl);
                }
                

                return Page();
            }
            catch (Exception ex)
            {



                // Don't handle the exception here - let the ExternalApiExceptionFilter handle it
                // This ensures that API errors get proper ModelState treatment
                throw;
            }
        }

        public async Task<IActionResult> OnPostDownloadFileAsync()
        {
            // Simple fix: Ensure Template is not null to prevent NullReferenceException
            if (Template == null)
            {
                Template = new FormTemplate 
                { 
                    TemplateId = "dummy", 
                    TemplateName = "dummy", 
                    Description = "dummy", 
                    TaskGroups = new List<TaskGroup>() 
                }; // Create empty template to prevent null reference

            }
            
            var applicationId = Request.Form["ApplicationId"].ToString();
            var fileIdStr = Request.Form["FileId"].ToString();
            
            if (!Guid.TryParse(applicationId, out var appId))
            {
                return NotFound();
            }
            if (!Guid.TryParse(fileIdStr, out var fileId))
            {
                return NotFound();
            }

            var fileResponse = await fileUploadService.DownloadFileAsync(fileId, appId);

            // Extract content type
            var contentType = fileResponse.Headers.TryGetValue("Content-Type", out var ct)
                ? ct.FirstOrDefault()
                : "application/octet-stream";

            string fileName = "downloadedfile";
            if (fileResponse.Headers.TryGetValue("Content-Disposition", out var cd))
            {
                var disposition = cd.FirstOrDefault();
                if (!string.IsNullOrEmpty(disposition))
                {
                    var fileNameMatch = System.Text.RegularExpressions.Regex.Match(
                        disposition,
                        @"filename\*=UTF-8''(?<fileName>.+)|filename=""?(?<fileName>[^\"";]+)""?"
                    );
                    if (fileNameMatch.Success)
                        fileName = System.Net.WebUtility.UrlDecode(fileNameMatch.Groups["fileName"].Value);
                }
            }

            return File(fileResponse.Stream, contentType, fileName);
        }

        public async Task<IActionResult> OnPostDeleteFileAsync()
        {
            // Clear any validation errors from previous POST requests
            // Without this, ModelState errors prevent confirmation from showing
            ModelState.Clear();
            
            // Simple fix: Ensure Template is not null to prevent NullReferenceException
            if (Template == null)
            {
                Template = new FormTemplate 
                { 
                    TemplateId = "dummy", 
                    TemplateName = "dummy", 
                    Description = "dummy", 
                    TaskGroups = new List<TaskGroup>() 
                }; // Create empty template to prevent null reference

            }
            
            var applicationId = Request.Form["ApplicationId"].ToString();
            var returnUrl = Request.Form["ReturnUrl"].ToString();
            var fileIdStr = Request.Form["FileId"].ToString();
            var fieldId = Request.Form["FieldId"].ToString();
            
            if (!Guid.TryParse(applicationId, out var appId))
                return NotFound();
                
            if (!Guid.TryParse(fileIdStr, out var fileId))
            {
                ModelState.AddModelError("FileId", "Invalid file ID.");
                
                // If we have a return URL, redirect back with error
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                
                return Page();
            }

            bool isConfirmed = Request.Query.ContainsKey("confirmed") && Request.Query["confirmed"] == "true";
            
            if (!isConfirmed)
            {
                _logger.LogInformation("DeleteFile handler executing for validation - file will not be deleted yet");
                return Redirect(returnUrl);
            }
            
            var addRequest = new AddNotificationRequest
            {
                Message = string.Empty, // set later when known
                Category = "file-upload",
                Context = $"file-delete|{fileId}",
                Type = NotificationType.Success,
                AutoDismiss = false,
                ReplaceExistingContext = false
            };

            try
            {
                await fileUploadService.DeleteFileAsync(fileId, appId);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to delete file {FileId} for application {ApplicationId}", fileId, appId);
                throw;
            }
            
            SuccessMessage = "File deleted.";

            var currentFieldFiles = _formFileFieldService.GetFiles(new FormFileFieldContext(appId, FlowId, InstanceId), fieldId).ToList();
            currentFieldFiles.RemoveAll(f => f.Id == fileId);
            
            _formFileFieldService.SaveFiles(new FormFileFieldContext(appId, FlowId, InstanceId), fieldId, currentFieldFiles);
            await SaveUploadedFilesToResponseAsync(appId, fieldId, currentFieldFiles);
            
            // If we have a return URL (from partial form), redirect back
            if (!string.IsNullOrEmpty(returnUrl))
            {
                //  Send notification for successful delete
                addRequest.Message = SuccessMessage;
                await TryCreateFileNotificationAsync(addRequest);

                
                return Redirect(returnUrl);
            }

            return Page();
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

        /// <summary>
        /// Filters out any infected files from the given list using the Redis blacklist.
        /// This ensures infected files are never shown or re-saved, regardless of where they come from.
        /// Uses direct key lookup instead of KEYS command for better reliability and performance.
        /// Checks both file ID-based and filename-based blacklists.
        /// </summary>
        private async Task RefreshFileValidationGateAsync()
        {
            FileValidationBlocksSubmit = false;
            FileValidationBlockingFiles = [];

            if (!ApplicationId.HasValue)
                return;

            try
            {
                var gate = await _applicationsClient.GetFileValidationGateAsync(ApplicationId.Value);
                FileValidationBlocksSubmit = !gate.CanSubmit;
                FileValidationBlockingFiles = gate.BlockingFiles ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not evaluate file validation gate for application {ApplicationId}", ApplicationId);
            }
        }

        public List<UploadDto> FilterInfectedFilesFromList(List<UploadDto> files) =>
            _infectedUploadFilter.FilterList(
                files,
                ApplicationId?.ToString() ?? HttpContext.Session.GetString(FormSessionKeys.ApplicationId));

        private async Task SaveUploadedFilesToResponseAsync(Guid appId, string fieldId, IReadOnlyList<UploadDto> files)
        {
            if (string.IsNullOrEmpty(fieldId))
            {
                return;
            }

            // Save files to database
            // NOTE: This is called by DELETE handler to persist deletions
            // It is NOT called by UPLOAD handler (to give scanner time to process)
            var json = JsonSerializer.Serialize(files);
            var data = new Dictionary<string, object> { { fieldId, json } };

            await _applicationResponseService.SaveApplicationResponseAsync(appId, data);
        }

        /// <summary>
        /// Populates Data dictionary with files from session for upload fields so they display on GET.
        /// Also cleans up session by removing any infected files that have been blacklisted.
        /// </summary>
        private void PopulateUploadFieldsFromSession()
        {
            if (CurrentPage == null || !ApplicationId.HasValue)
                return;

            // Find all upload fields on the current page
            var uploadFields = CurrentPage.Fields
                .Where(f => f.Type == "complexField" 
                    && f.ComplexField != null 
                    && _complexFieldConfigurationService.GetConfiguration(f.ComplexField.Id).FieldType.Equals("upload", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var field in uploadFields)
            {
                var fieldId = field.FieldId;
                
                // Get files from session (this already filters out infected files)
                var files = _formFileFieldService.GetFiles(FileFieldContext, fieldId);
                
                // Update session with the filtered list to remove infected files from session
                // This ensures ContainsFileName won't find infected files
                _formFileFieldService.SaveFiles(FileFieldContext, fieldId, files.ToList());
                
                if (files.Any())
                {
                    // Serialize files to JSON and populate Data so the view can display them
                    var filesJson = JsonSerializer.Serialize(files);
                    Data[fieldId] = filesJson;
                }
            }
        }

        private void MergeFlowProgressIntoFormDataForSummary()
        {
            if (CurrentTask?.Summary?.Mode?.Equals("multiCollectionFlow", StringComparison.OrdinalIgnoreCase) != true
                || CurrentTask.Summary?.Flows == null)
                return;

            foreach (var flow in CurrentTask.Summary.Flows)
            {
                if (!FormData.TryGetValue(flow.FieldId, out var val) || string.IsNullOrWhiteSpace(val?.ToString()))
                    continue;

                var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(val.ToString()!) ?? new();
                var changed = false;

                foreach (var item in items)
                {
                    if (!item.TryGetValue("id", out var idObj)) continue;
                    var instanceId = idObj?.ToString();
                    if (string.IsNullOrWhiteSpace(instanceId)) continue;

                    var progress = _collectionFlowProgressStore.Load(flow.FlowId, instanceId);
                    if (!progress.Any()) continue;

                    foreach (var kv in progress)
                    {
                        item[kv.Key] = kv.Value;
                    }
                    changed = true;
                }

                if (changed)
                {
                    var updatedJson = JsonSerializer.Serialize(items);
                    FormData[flow.FieldId] = updatedJson;
                    Data[flow.FieldId] = updatedJson; // keep Data in sync for views
                }
            }
        }

        #endregion

        #region Form Error Store Helper Methods

        /// <summary>
        /// Gets a unique context key for storing form errors in session
        /// </summary>
        /// <returns>Form error context key</returns>
        private string GetFormErrorContextKey()
        {
            return $"{ReferenceNumber}_{TaskId}_{CurrentPageId}";
        }

        /// <summary>
        /// Restores previously saved form errors from session and applies them to ModelState
        /// </summary>
        private void RestoreFormErrors()
        {
            try
            {
                var contextKey = GetFormErrorContextKey();
                var (fieldErrors, generalError) = _formErrorStore.Load(contextKey, clearAfterRead: true);
                
                if (fieldErrors.Any())
                {
                    foreach (var kvp in fieldErrors)
                    {
                        foreach (var error in kvp.Value)
                        {
                            ModelState.AddModelError(kvp.Key, error);
                        }
                    }
                    _logger.LogInformation("DEBUG: Restored {ErrorCount} field errors from FormErrorStore with key: {ContextKey}", 
                        fieldErrors.Sum(x => x.Value.Count), contextKey);
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

        #endregion

        #region Collection Item File Cleanup Helper Methods

        /// <summary>
        /// Deletes all files associated with a collection item when the item is removed.
        /// Iterates through all fields in the item data and deletes any files found.
        /// </summary>
        /// <param name="applicationId">The application ID</param>
        /// <param name="itemData">The collection item data dictionary</param>
        /// <returns>The number of files deleted</returns>
        private async Task<int> DeleteFilesFromCollectionItemAsync(Guid applicationId, Dictionary<string, object>? itemData)
        {
            if (itemData == null)
            {
                return 0;
            }

            int deletedCount = 0;

            foreach (var kvp in itemData)
            {
                // Skip the 'id' field and any non-string values
                if (kvp.Key == "id" || kvp.Value == null)
                {
                    continue;
                }

                try
                {
                    var valueStr = kvp.Value?.ToString();
                    
                    // Skip empty values or values that don't look like JSON arrays
                    if (string.IsNullOrEmpty(valueStr) || !valueStr.TrimStart().StartsWith("["))
                    {
                        continue;
                    }

                    // Try to parse as file list (UploadDto)
                    var files = JsonSerializer.Deserialize<List<UploadDto>>(valueStr);
                    if (files != null && files.Any())
                    {
                        foreach (var file in files)
                        {
                            try
                            {
                                await fileUploadService.DeleteFileAsync(file.Id, applicationId);
                                deletedCount++;
                                _logger.LogInformation(
                                    "Deleted file {FileId} ({FileName}) from removed collection item in application {ApplicationId}",
                                    file.Id,
                                    file.OriginalFileName,
                                    applicationId);
                            }
                            catch (Exception ex)
                            {
                                // Log but don't fail the entire operation - file may already be deleted
                                _logger.LogWarning(
                                    ex,
                                    "Failed to delete file {FileId} from collection item - file may already be deleted",
                                    file.Id);
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    // Not a file list, skip this field
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing field {FieldKey} for file cleanup", kvp.Key);
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Successfully deleted {DeletedCount} file(s) from removed collection item in application {ApplicationId}",
                    deletedCount,
                    applicationId);
            }

            return deletedCount;
        }

        #endregion

        #region Helper Methods for Field Requirement

        /// <summary>
        /// Gets a field from a task by field ID
        /// </summary>
        /// <param name="task">The task to search</param>
        /// <param name="fieldId">The field ID to find</param>
        /// <returns>The field if found, otherwise null</returns>
        private Field? GetFieldFromTask(Domain.Models.Task task, string fieldId)
        {
            if (task?.Pages == null) return null;

            foreach (var page in task.Pages)
            {
                if (page?.Fields == null) continue;

                var field = page.Fields.FirstOrDefault(f => f.FieldId == fieldId);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        #endregion

    }
}








