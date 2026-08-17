namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

public sealed class CollectionFlowItemViewModel
{
    public required string ItemId { get; init; }
    public required string Title { get; init; }
    public required CollectionItemRemoveViewModel Remove { get; init; }
    public required SummaryRowViewModel HeaderRow { get; init; }
    public required IReadOnlyList<SummaryRowViewModel> Rows { get; init; }
}
