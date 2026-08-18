namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

public sealed class SummaryValueViewModel
{
    public SummaryDisplayKind Kind { get; init; }
    public string? Text { get; init; }
    public string? Html { get; init; }
    public IReadOnlyList<string> HtmlItems { get; init; } = [];
    public IReadOnlyList<SummaryFileLinkViewModel> Files { get; init; } = [];
    public bool WrapFilesInDivs { get; init; }
    public IReadOnlyList<string> Checkboxes { get; init; } = [];
    public string? StatusText { get; init; }
    public bool StatusIsSigned { get; init; }

    public static SummaryValueViewModel NotAnswered { get; } = new() { Kind = SummaryDisplayKind.NotAnswered };

    public static SummaryValueViewModel Empty { get; } = new() { Kind = SummaryDisplayKind.Empty };

    public static SummaryValueViewModel FromHtml(string html) =>
        new() { Kind = SummaryDisplayKind.Html, Html = html };

    public static SummaryValueViewModel FromText(string text) =>
        new() { Kind = SummaryDisplayKind.Text, Text = text };

    public static SummaryValueViewModel FromHtmlList(IReadOnlyList<string> items) =>
        new() { Kind = SummaryDisplayKind.HtmlList, HtmlItems = items };

    public static SummaryValueViewModel FromCheckboxes(IReadOnlyList<string> items) =>
        new() { Kind = SummaryDisplayKind.Checkboxes, Checkboxes = items };

    public static SummaryValueViewModel FromAutocompleteHtml(string html) =>
        new() { Kind = SummaryDisplayKind.AutocompleteHtml, Html = html };

    public static SummaryValueViewModel FromStatusTag(string status) =>
        new()
        {
            Kind = SummaryDisplayKind.StatusTag,
            StatusText = status,
            StatusIsSigned = string.Equals(status, "Signed", StringComparison.Ordinal)
        };

    public static SummaryValueViewModel FromFiles(
        IReadOnlyList<SummaryFileLinkViewModel> files,
        bool wrapFilesInDivs) =>
        new()
        {
            Kind = SummaryDisplayKind.UploadFiles,
            Files = files,
            WrapFilesInDivs = wrapFilesInDivs
        };
}
