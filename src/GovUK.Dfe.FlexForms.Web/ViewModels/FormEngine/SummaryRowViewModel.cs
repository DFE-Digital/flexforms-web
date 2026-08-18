namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

public sealed class SummaryRowViewModel
{
    public required string Key { get; init; }
    public bool KeyIsBold { get; init; }
    public bool ShowSeparator { get; init; }
    public required SummaryValueViewModel Value { get; init; }
    public string? ChangeUrl { get; init; }
    public string? ChangeHiddenText { get; init; }
    public CollectionItemRemoveViewModel? Remove { get; init; }
}
