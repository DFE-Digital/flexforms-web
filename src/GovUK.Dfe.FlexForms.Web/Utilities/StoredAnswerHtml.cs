using System.Text.Encodings.Web;
using GovUK.Dfe.FlexForms.Web.Pages.FormEngine;

namespace GovUK.Dfe.FlexForms.Web.Utilities;

/// <summary>
/// Encodes stored form answers at render time. Web posts are already HTML-encoded;
/// API-written values may not be. Decode then re-encode so neither is double-encoded,
/// then restore line breaks as <c>&lt;br&gt;</c>.
/// </summary>
public static class StoredAnswerHtml
{
    public static string ToSafeLineBreaks(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var decoded = DisplayHelpers.UnsanitiseHtmlInput(value);
        var lines = decoded.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        return string.Join("<br/>", lines.Select(HtmlEncoder.Default.Encode));
    }
}
