using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

public sealed class PreviewSubmitViewModel
{
    public bool IsEditable { get; init; }
    public bool IsLeadApplicant { get; init; }
    public bool SubmitDisabledByConfig { get; init; }
    public string? DisabledBannerText { get; init; }
    public string? DisabledHelpText { get; init; }
    public bool FileValidationBlocksSubmit { get; init; }
    public IReadOnlyList<FileValidationBlockDto> BlockingFiles { get; init; } = [];
    public bool IncludePreviewQuery { get; init; }

    public bool ShowSubmitSection => IsEditable && IsLeadApplicant;

    public bool ShowLeadApplicantInset => IsEditable && !IsLeadApplicant;
}
