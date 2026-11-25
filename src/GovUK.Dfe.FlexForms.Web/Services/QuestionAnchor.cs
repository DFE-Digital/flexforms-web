using System.Text.RegularExpressions;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Single source of truth for the stable, visible anchor that identifies a question on a form page.
/// <para>
/// <see cref="FieldRendererService"/> wraps every rendered field in an element carrying <see cref="ElementId"/>,
/// and summary "Change" links target it via <see cref="Fragment"/>. Unlike <c>Data_{FieldId}</c> (the input id),
/// the wrapper exists for every field type: autocomplete renders its <c>Data_</c> id on a hidden input and
/// uploads render no such element at all (and no input once the upload limit is reached).
/// </para>
/// </summary>
public static class QuestionAnchor
{
    public const string IdPrefix = "question-";

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>The HTML id for the element wrapping the question for <paramref name="fieldId"/>.</summary>
    public static string ElementId(string fieldId) =>
        IdPrefix + Whitespace.Replace(fieldId.Trim(), "-");

    /// <summary>The URL fragment (including the leading <c>#</c>) targeting <see cref="ElementId"/>.</summary>
    public static string Fragment(string fieldId) =>
        "#" + Uri.EscapeDataString(ElementId(fieldId));
}
