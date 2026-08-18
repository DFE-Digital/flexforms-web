namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

public sealed class PreviewGroupViewModel
{
    public required string GroupName { get; init; }
    public required string TestId { get; init; }
    public required IReadOnlyList<PreviewTaskCardViewModel> Tasks { get; init; }
}
