using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Notifications;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Web.Extensions;
using GovUK.Dfe.FlexForms.Web.Interfaces;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.FormEngine;

/// <summary>
/// Standalone upload URL. Live forms post to <see cref="RenderFormModel"/>; this page reuses the same file use cases.
/// </summary>
public class UploadFileModel(
    IUploadFormFile uploadFormFile,
    IDeleteFormFile deleteFormFile,
    IDownloadFormFile downloadFormFile,
    IFormFileFieldService formFileFieldService,
    IFileUploadService fileUploadService,
    IApplicationResponseService applicationResponseService,
    INotificationsClient notificationsClient,
    IFormErrorStore formErrorStore,
    IRequestAppConfiguration requestConfiguration,
    ILogger<UploadFileModel> logger)
    : PageModel
{
    private string ApplicationContext =>
        requestConfiguration["ApplicationName"]
        ?? requestConfiguration["TenantName"]
        ?? throw new InvalidOperationException(
            "ApplicationName (or TenantName) is required in tenant configuration for notifications.");

    [BindProperty(SupportsGet = true)] public string ApplicationId { get; set; } = string.Empty;
    [BindProperty(SupportsGet = true)] public string FieldId { get; set; } = string.Empty;
    [BindProperty(SupportsGet = true, Name = "referenceNumber")] public string ReferenceNumber { get; set; } = string.Empty;
    [BindProperty(SupportsGet = true, Name = "taskId")] public string TaskId { get; set; } = string.Empty;
    [BindProperty(SupportsGet = true, Name = "pageId")] public string CurrentPageId { get; set; } = string.Empty;
    [BindProperty] public string ReturnUrl { get; set; } = string.Empty;
    [BindProperty] public string FlowId { get; set; } = string.Empty;
    [BindProperty] public string InstanceId { get; set; } = string.Empty;
    public IReadOnlyList<UploadDto> Files { get; set; } = [];
    public string SuccessMessage { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    private bool IsCollectionFlow => !string.IsNullOrEmpty(FlowId) && !string.IsNullOrEmpty(InstanceId);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Guid.TryParse(ApplicationId, out var appId))
            return NotFound();

        Files = await GetFilesForFieldAsync(appId, FieldId);
        return Page();
    }

    public override void OnPageHandlerExecuted(PageHandlerExecutedContext context)
    {
        base.OnPageHandlerExecuted(context);

        if (!ModelState.IsValid && !string.IsNullOrEmpty(FieldId))
        {
            formErrorStore.Save(FieldId, ModelState);
            if (!string.IsNullOrEmpty(ReturnUrl))
                context.Result = new RedirectResult(ReturnUrl);
        }
    }

    public async Task<IActionResult> OnPostUploadFileAsync()
    {
        if (!IsCollectionFlow)
        {
            ModelState.Remove("FlowId");
            ModelState.Remove("InstanceId");
        }

        if (!Guid.TryParse(ApplicationId, out var appId))
            return NotFound();

        var file = Request.Form.Files["UploadFile"];
        var hasFile = file is { Length: > 0 };
        await using var stream = hasFile ? file!.OpenReadStream() : Stream.Null;

        var state = CaptureWorkState();
        var outcome = await uploadFormFile.ExecuteAsync(state, new UploadFormFileRequest(
            appId,
            FieldId,
            ReturnUrl,
            Request.Form["UploadDescription"].ToString(),
            stream,
            file?.FileName ?? string.Empty,
            file?.ContentType,
            ErrorContextKey,
            hasFile));

        await PersistFieldFilesIfStandaloneAsync(appId, outcome);
        await TryNotifyFileOperationAsync(outcome);
        return MapOutcome(outcome);
    }

    public async Task<IActionResult> OnPostDeleteFileAsync()
    {
        if (!Guid.TryParse(ApplicationId, out var appId))
            return NotFound();

        var fileIdStr = Request.Form["FileId"].ToString();
        if (!Guid.TryParse(fileIdStr, out var fileId))
        {
            ErrorMessage = FormEngineMessages.InvalidFileId;
            if (!string.IsNullOrEmpty(FieldId))
                formErrorStore.Save(FieldId, ModelState, ErrorMessage);

            return string.IsNullOrEmpty(ReturnUrl) ? Page() : Redirect(ReturnUrl);
        }

        var outcome = await deleteFormFile.ExecuteAsync(
            CaptureWorkState(),
            new DeleteFormFileRequest(appId, fileId, FieldId, ReturnUrl, Confirmed: true));
        await TryNotifyFileOperationAsync(outcome);
        return MapOutcome(outcome);
    }

    public async Task<IActionResult> OnPostDownloadFileAsync()
    {
        if (!Guid.TryParse(ApplicationId, out var appId))
            return NotFound();
        var fileIdStr = Request.Form["FileId"].ToString();
        if (!Guid.TryParse(fileIdStr, out var fileId))
            return NotFound();

        var outcome = await downloadFormFile.ExecuteAsync(
            CaptureWorkState(),
            new DownloadFormFileRequest(appId, fileId));
        return MapOutcome(outcome);
    }

    private async Task<IReadOnlyList<UploadDto>> GetFilesForFieldAsync(Guid appId, string fieldId)
    {
        var files = formFileFieldService.GetFiles(new FormFileFieldContext(appId, FlowId, InstanceId), fieldId).ToList();
        try
        {
            var allDbFiles = await fileUploadService.GetFilesForApplicationAsync(appId);
            return files.Where(sf => allDbFiles.Any(dbf => dbf.Id == sf.Id)).ToList();
        }
        catch (ExternalApplicationsException ex) when (ex.StatusCode is 401 or 403)
        {
            return files;
        }
    }

    private FormEngineWorkState CaptureWorkState() =>
        new()
        {
            ReferenceNumber = ReferenceNumber,
            TaskId = TaskId,
            CurrentPageId = CurrentPageId,
            FlowId = FlowId,
            InstanceId = InstanceId
        };

    private string ErrorContextKey => $"{ReferenceNumber}_{TaskId}_{CurrentPageId}";

    private IActionResult MapOutcome(FormEngineOutcome outcome)
    {
        foreach (var key in outcome.ModelStateKeysToRemove)
            ModelState.Remove(key);

        if (outcome.ClearModelState)
            ModelState.Clear();

        if (outcome.Errors.Count > 0)
            new FormValidationResult(outcome.Errors).ApplyTo(ModelState);

        if (outcome.SuccessMessage != null)
            SuccessMessage = outcome.SuccessMessage;

        if (outcome.ErrorMessage != null)
            ErrorMessage = outcome.ErrorMessage;

        if (outcome.Files != null)
            Files = outcome.Files;

        foreach (var key in outcome.ErrorStoreKeysToClear)
            formErrorStore.Clear(key);

        if (outcome.PersistErrors && !string.IsNullOrEmpty(outcome.ErrorContextKey))
            formErrorStore.Save(outcome.ErrorContextKey, ModelState);

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

    private async Task PersistFieldFilesIfStandaloneAsync(Guid appId, FormEngineOutcome outcome)
    {
        if (outcome.Files == null || string.IsNullOrEmpty(FieldId) || IsCollectionFlow)
            return;

        var json = System.Text.Json.JsonSerializer.Serialize(outcome.Files);
        await applicationResponseService.SaveApplicationResponseAsync(appId, new Dictionary<string, object> { { FieldId, json } });
    }

    private async Task TryNotifyFileOperationAsync(FormEngineOutcome outcome)
    {
        if (string.IsNullOrEmpty(outcome.SuccessMessage) || string.IsNullOrEmpty(outcome.NotificationContext))
            return;

        try
        {
            await notificationsClient.CreateNotificationAsync(new AddNotificationRequest
            {
                Message = outcome.SuccessMessage,
                Category = "file-upload",
                Context = NotificationScopeContext.PrefixDetail(ApplicationContext, outcome.NotificationContext),
                Type = NotificationType.Success,
                AutoDismiss = false,
                AutoDismissSeconds = outcome.NotificationContext.StartsWith("file-upload|", StringComparison.Ordinal)
                    ? 5
                    : 0,
                ReplaceExistingContext = false
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "File operation succeeded but notification could not be created");
        }
    }
}
