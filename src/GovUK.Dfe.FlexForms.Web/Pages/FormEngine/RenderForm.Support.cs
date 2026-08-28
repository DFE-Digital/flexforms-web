using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Notifications;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Extensions;
using GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.Pages.FormEngine;

public partial class RenderFormModel
{
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

    private void RestoreConfirmedFormData()
    {
        if (!Request.Query.ContainsKey("confirmed") || Request.Query["confirmed"] != "true")
            return;

        var confirmedDataJson = TempData["ConfirmedFormData"]?.ToString();
        var confirmedHandler = TempData["ConfirmedHandler"]?.ToString();
        if (string.IsNullOrEmpty(confirmedDataJson))
            return;

        try
        {
            var confirmedData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(confirmedDataJson);
            if (confirmedData == null)
                return;

            foreach (var kvp in confirmedData)
                Data[kvp.Key] = kvp.Value;
            _logger.LogInformation("Restored {Count} confirmed form fields for handler {Handler}",
                confirmedData.Count, confirmedHandler);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize confirmed form data");
        }
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

    private async Task TryNotifyFileOperationAsync(FormEngineOutcome outcome)
    {
        if (string.IsNullOrEmpty(outcome.SuccessMessage) || string.IsNullOrEmpty(outcome.NotificationContext))
            return;

        var isUpload = outcome.NotificationContext.StartsWith("file-upload|", StringComparison.Ordinal);

        await TryCreateFileNotificationAsync(new AddNotificationRequest
        {
            Message = outcome.SuccessMessage,
            Category = isUpload ? "file-upload" : "file-delete",
            Context = outcome.NotificationContext,
            Type = NotificationType.Success,
            AutoDismiss = false,
            // API rejects AutoDismissSeconds <= 0 when the property is set.
            AutoDismissSeconds = 5,
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
