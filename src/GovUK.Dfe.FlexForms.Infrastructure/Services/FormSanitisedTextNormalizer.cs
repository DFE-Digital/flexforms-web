using System.Text.RegularExpressions;
using System.Web;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Reverses the sanitisation applied when form text fields are posted (<c>DisplayHelpers.SanitiseHtmlInput</c> in the web layer)
/// so server-side checks (for example max length) use the same logical string as the GOV.UK character-count and the textarea value.
/// </summary>
public static partial class FormSanitisedTextNormalizer
{
    /// <summary>
    /// Replaces line-break markers with newlines and HTML-decodes entities. Use only for transient validation; the sanitised value remains the source of truth for storage.
    /// </summary>
    /// <param name="sanitisedOrPlain">Field value after post binding (sanitised) or plain text (for example in unit tests).</param>
    /// <returns>Plain text aligned with what the user typed in the browser.</returns>
    public static string ToPlainTextForCharacterCountValidation(string sanitisedOrPlain)
    {
        if (string.IsNullOrEmpty(sanitisedOrPlain))
        {
            return sanitisedOrPlain;
        }

        return HttpUtility.HtmlDecode(LineBreakTagRegex().Replace(sanitisedOrPlain, "\n"));
    }

    [GeneratedRegex(@"<br\s*/?>")]
    private static partial Regex LineBreakTagRegex();
}
