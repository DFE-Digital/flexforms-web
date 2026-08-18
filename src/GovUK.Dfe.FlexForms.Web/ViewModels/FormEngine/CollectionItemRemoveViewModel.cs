namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

public sealed class CollectionItemRemoveViewModel
{
    public required string ReferenceNumber { get; init; }
    public required string TaskId { get; init; }
    public required string FlowId { get; init; }
    public required string FieldId { get; init; }
    public required string ItemId { get; init; }
    public required string ItemTitle { get; init; }
    public required string TaskName { get; init; }
    public required string ConfirmationTitle { get; init; }
    public required string RequiredMessage { get; init; }
    public required string ButtonId { get; init; }
}
