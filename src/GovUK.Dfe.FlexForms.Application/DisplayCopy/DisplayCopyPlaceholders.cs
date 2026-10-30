namespace GovUK.Dfe.FlexForms.Application.DisplayCopy;

/// <summary>
/// Substitutes {template_name}, {singular}, {plural} and {contributor_email} in admin-authored copy.
/// </summary>
public static class DisplayCopyPlaceholders
{
    public static string Apply(
        string copy,
        string? templateName,
        string singular,
        string plural,
        string? contributorEmail)
    {
        if (string.IsNullOrEmpty(copy))
            return copy;

        var result = copy;
        result = Replace(result, "{template_name}", templateName);
        result = Replace(result, "{singular}", singular);
        result = Replace(result, "{plural}", plural);
        result = Replace(result, "{contributor_email}", contributorEmail);
        return result;
    }

    private static string Replace(string copy, string token, string? value)
    {
        if (!copy.Contains(token, StringComparison.OrdinalIgnoreCase))
            return copy;

        return copy.Replace(token, value?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
