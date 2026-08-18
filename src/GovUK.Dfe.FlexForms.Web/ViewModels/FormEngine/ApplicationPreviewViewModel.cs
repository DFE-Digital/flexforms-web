namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

public sealed class ApplicationPreviewViewModel
{
    public required string ReferenceNumber { get; init; }
    public required IReadOnlyList<PreviewGroupViewModel> Groups { get; init; }
    public required PreviewSubmitViewModel Submit { get; init; }
}
