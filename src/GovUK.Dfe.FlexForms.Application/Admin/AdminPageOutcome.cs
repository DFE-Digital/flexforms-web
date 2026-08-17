using GovUK.Dfe.FlexForms.Application.Validation;

namespace GovUK.Dfe.FlexForms.Application.Admin;

public enum AdminPageOutcomeKind
{
    StayOnPage,
    RedirectToPage,
    FileDownload
}

/// <summary>
/// HTTP-agnostic result of an Admin use case. The PageModel maps this to <c>Page()</c> / <c>RedirectToPage()</c> / <c>File()</c>.
/// </summary>
public sealed class AdminPageOutcome
{
    public AdminPageOutcomeKind Kind { get; init; }

    public string? SuccessMessage { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<FormValidationError> Errors { get; init; } = [];

    public bool RefreshLocalCaches { get; init; }

    public byte[]? FileBytes { get; init; }

    public string? FileContentType { get; init; }

    public string? FileDownloadName { get; init; }

    public IReadOnlyDictionary<string, string?> RouteValues { get; init; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);

    public IReadOnlyList<string> ModelStateKeysToRemove { get; init; } = [];

    public bool ClearModelState { get; init; }

    public static AdminPageOutcome Stay(
        string? errorMessage = null,
        string? successMessage = null,
        IReadOnlyList<FormValidationError>? errors = null,
        IReadOnlyList<string>? modelStateKeysToRemove = null,
        bool clearModelState = false,
        bool refreshLocalCaches = false) =>
        new()
        {
            Kind = AdminPageOutcomeKind.StayOnPage,
            ErrorMessage = errorMessage,
            SuccessMessage = successMessage,
            Errors = errors ?? [],
            ModelStateKeysToRemove = modelStateKeysToRemove ?? [],
            ClearModelState = clearModelState,
            RefreshLocalCaches = refreshLocalCaches
        };

    public static AdminPageOutcome Redirect(
        string? successMessage = null,
        string? errorMessage = null,
        bool refreshLocalCaches = false,
        IReadOnlyDictionary<string, string?>? routeValues = null) =>
        new()
        {
            Kind = AdminPageOutcomeKind.RedirectToPage,
            SuccessMessage = successMessage,
            ErrorMessage = errorMessage,
            RefreshLocalCaches = refreshLocalCaches,
            RouteValues = routeValues ?? new Dictionary<string, string?>(StringComparer.Ordinal)
        };

    public static AdminPageOutcome File(byte[] bytes, string contentType, string fileName) =>
        new()
        {
            Kind = AdminPageOutcomeKind.FileDownload,
            FileBytes = bytes,
            FileContentType = contentType,
            FileDownloadName = fileName
        };
}
