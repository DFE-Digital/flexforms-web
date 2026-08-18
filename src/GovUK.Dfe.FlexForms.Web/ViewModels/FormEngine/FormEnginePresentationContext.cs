using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

public sealed class FormEnginePresentationContext
{
    public required FormTemplate Template { get; init; }
    public required Dictionary<string, object> FormData { get; init; }
    public required string ReferenceNumber { get; init; }
    public string TaskId { get; init; } = string.Empty;
    public Guid? ApplicationId { get; init; }
    public string? InfectedFilterApplicationId { get; init; }
    public bool IsEditable { get; init; }
    public bool IsLeadApplicant { get; init; }
    public bool SubmitDisabledByConfig { get; init; }
    public string? SubmitDisabledBannerText { get; init; }
    public string? SubmitDisabledHelpText { get; init; }
    public bool FileValidationBlocksSubmit { get; init; }
    public IReadOnlyList<FileValidationBlockDto> BlockingFiles { get; init; } = [];
    public bool IncludePreviewQuery { get; init; }
    public required Action<Dictionary<string, object>, IEnumerable<string>> EnsureItemFieldVisibility { get; init; }
    public required Func<string, Dictionary<string, object>, bool> IsFieldHiddenForItem { get; init; }
    public required Func<string, bool> IsFieldHidden { get; init; }
}
