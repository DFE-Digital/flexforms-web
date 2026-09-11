namespace GovUK.Dfe.FlexForms.Application.Dashboard;

/// <summary>
/// Substitutes the placeholders an admin can use in configurable dashboard copy,
/// for example "Your applications for {template_name}".
/// </summary>
public static class DashboardCopyPlaceholders
{
    public const string TemplateNameToken = "{template_name}";

    public static string Apply(string copy, string? templateName)
    {
        if (string.IsNullOrEmpty(copy)
            || !copy.Contains(TemplateNameToken, StringComparison.OrdinalIgnoreCase))
        {
            return copy;
        }

        var name = templateName?.Trim() ?? string.Empty;
        var substituted = copy.Replace(TemplateNameToken, name, StringComparison.OrdinalIgnoreCase);

        // Dropping the token leaves the spacing that surrounded it behind.
        return name.Length == 0 ? CollapseSpaces(substituted) : substituted;
    }

    private static string CollapseSpaces(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
