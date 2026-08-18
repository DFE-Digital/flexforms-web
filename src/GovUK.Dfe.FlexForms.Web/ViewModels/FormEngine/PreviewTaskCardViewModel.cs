namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

public sealed class PreviewTaskCardViewModel
{
    public required string TaskId { get; init; }
    public required string TaskName { get; init; }
    public required string TestId { get; init; }
    public required string ChangeUrl { get; init; }
    public required IReadOnlyList<SummaryRowViewModel> Rows { get; init; }
}
