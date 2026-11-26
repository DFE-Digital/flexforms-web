namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Single source of truth for the stable, visible anchor that identifies a question on a form page.
/// <para>
/// <see cref="FieldRendererService"/> wraps every rendered field in an element carrying <see cref="ElementId"/>,
/// and summary "Change" links target it via <see cref="Fragment"/>. Unlike <c>Data_{FieldId}</c> (the input id),
/// the wrapper exists for every field type: autocomplete renders its <c>Data_</c> id on a hidden input and
/// uploads render no such element at all (and no input once the upload limit is reached).
/// </para>
/// <para>
/// The original field id is percent-encoded without trimming or normalisation, so distinct field ids
/// (for example <c>Lead attendee</c> and <c>Lead-attendee</c>) always yield distinct anchors, and the
/// result contains no whitespace (invalid in an HTML id).
/// </para>
/// </summary>
public static class QuestionAnchor
{
    public const string IdPrefix = "question-";

    /// <summary>The HTML id for the element wrapping the question for <paramref name="fieldId"/>.</summary>
    public static string ElementId(string fieldId) =>
        IdPrefix + Uri.EscapeDataString(fieldId);

    /// <summary>
    /// The URL fragment (including the leading <c>#</c>) targeting <see cref="ElementId"/>.
    /// The id is already URL-safe (unreserved characters and <c>%XX</c>), so it is used as-is: browsers match
    /// the raw fragment against element ids first, and escaping it again would rely on their decoded fallback.
    /// </summary>
    public static string Fragment(string fieldId) =>
        "#" + ElementId(fieldId);
}
