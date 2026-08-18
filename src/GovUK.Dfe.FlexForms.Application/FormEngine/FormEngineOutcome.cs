using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

public enum FormEngineOutcomeKind
{
    StayOnPage,
    Redirect,
    RedirectToPage,
    NotFound,
    BadRequest,
    FileDownload
}

/// <summary>
/// HTTP-agnostic result of a form-engine use case. The PageModel maps this to <c>Page()</c> / <c>Redirect()</c>.
/// </summary>
public sealed class FormEngineOutcome
{
    public FormEngineOutcomeKind Kind { get; init; }

    public string? RedirectUrl { get; init; }

    public string? PageName { get; init; }

    public object? RouteValues { get; init; }

    public IReadOnlyList<FormValidationError> Errors { get; init; } = [];

    public bool ClearModelState { get; init; }

    public FormState? FormState { get; init; }

    public bool? IsTaskCompleted { get; init; }

    public string? SuccessMessage { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<UploadDto>? Files { get; init; }

    public bool FileValidationBlocksSubmit { get; init; }

    public IReadOnlyList<FileValidationBlockDto> BlockingFiles { get; init; } = [];

    public bool PersistErrors { get; init; }

    public string? ErrorContextKey { get; init; }

    public IReadOnlyList<string> ErrorStoreKeysToClear { get; init; } = [];

    public IReadOnlyList<string> ModelStateKeysToRemove { get; init; } = [];

    public bool ReloadFormData { get; init; }

    public FormConditionalState? ConditionalState { get; init; }

    public Stream? FileStream { get; init; }

    public string? FileContentType { get; init; }

    public string? FileDownloadName { get; init; }

    public string? NotificationContext { get; init; }

    public static FormEngineOutcome Stay(
        FormState? formState = null,
        IReadOnlyList<FormValidationError>? errors = null,
        bool clearModelState = false,
        bool? isTaskCompleted = null,
        bool reloadFormData = false,
        FormConditionalState? conditionalState = null,
        bool persistErrors = false,
        string? errorContextKey = null,
        string? errorMessage = null,
        string? successMessage = null,
        IReadOnlyList<UploadDto>? files = null,
        bool fileValidationBlocksSubmit = false,
        IReadOnlyList<FileValidationBlockDto>? blockingFiles = null,
        IReadOnlyList<string>? errorStoreKeysToClear = null,
        IReadOnlyList<string>? modelStateKeysToRemove = null,
        string? notificationContext = null) =>
        new()
        {
            Kind = FormEngineOutcomeKind.StayOnPage,
            FormState = formState,
            Errors = errors ?? [],
            ClearModelState = clearModelState,
            IsTaskCompleted = isTaskCompleted,
            ReloadFormData = reloadFormData,
            ConditionalState = conditionalState,
            PersistErrors = persistErrors,
            ErrorContextKey = errorContextKey,
            ErrorMessage = errorMessage,
            SuccessMessage = successMessage,
            Files = files,
            FileValidationBlocksSubmit = fileValidationBlocksSubmit,
            BlockingFiles = blockingFiles ?? [],
            ErrorStoreKeysToClear = errorStoreKeysToClear ?? [],
            ModelStateKeysToRemove = modelStateKeysToRemove ?? [],
            NotificationContext = notificationContext
        };

    public static FormEngineOutcome Redirect(
        string url,
        string? successMessage = null,
        IReadOnlyList<FormValidationError>? errors = null,
        bool persistErrors = false,
        string? errorContextKey = null,
        IReadOnlyList<UploadDto>? files = null,
        string? errorMessage = null,
        IReadOnlyList<string>? errorStoreKeysToClear = null,
        IReadOnlyList<string>? modelStateKeysToRemove = null,
        string? notificationContext = null) =>
        new()
        {
            Kind = FormEngineOutcomeKind.Redirect,
            RedirectUrl = url,
            SuccessMessage = successMessage,
            Errors = errors ?? [],
            PersistErrors = persistErrors,
            ErrorContextKey = errorContextKey,
            Files = files,
            ErrorMessage = errorMessage,
            ErrorStoreKeysToClear = errorStoreKeysToClear ?? [],
            ModelStateKeysToRemove = modelStateKeysToRemove ?? [],
            NotificationContext = notificationContext
        };

    public static FormEngineOutcome RedirectToPage(string pageName, object? routeValues = null) =>
        new()
        {
            Kind = FormEngineOutcomeKind.RedirectToPage,
            PageName = pageName,
            RouteValues = routeValues
        };

    public static FormEngineOutcome NotFound() =>
        new() { Kind = FormEngineOutcomeKind.NotFound };

    public static FormEngineOutcome BadRequest(string message) =>
        new()
        {
            Kind = FormEngineOutcomeKind.BadRequest,
            ErrorMessage = message
        };

    public static FormEngineOutcome File(Stream stream, string contentType, string fileName) =>
        new()
        {
            Kind = FormEngineOutcomeKind.FileDownload,
            FileStream = stream,
            FileContentType = contentType,
            FileDownloadName = fileName
        };
}
