namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

public sealed class CollectionFlowSectionViewModel
{
    public required string FlowId { get; init; }
    public required string Title { get; init; }
    public string? DescriptionHtml { get; init; }
    public required string ItemKind { get; init; }
    public required string ItemKindPlural { get; init; }
    public required string AddButtonLabel { get; init; }
    public required string AddButtonId { get; init; }
    public required string AddUrl { get; init; }
    public required string NoItemsHintId { get; init; }
    public bool CanAddMore { get; init; }
    public bool IsListStyle { get; init; }
    public required IReadOnlyList<CollectionFlowItemViewModel> Items { get; init; }
}
