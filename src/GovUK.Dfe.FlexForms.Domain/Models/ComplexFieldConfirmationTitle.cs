namespace GovUK.Dfe.FlexForms.Domain.Models;

/// <summary>
/// Builds the autocomplete confirmation heading. A blank template keeps
/// "Is this the right {name}?". <c>{name}</c> is replaced with the sentence name.
/// </summary>
public static class ComplexFieldConfirmationTitle
{
    public const string DefaultTemplate = "Is this the right {name}?";

    public static string Format(string? template, string? name)
    {
        var pattern = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template.Trim();
        return pattern.Replace("{name}", name?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
