namespace GovUK.Dfe.FlexForms.Domain.Templates;

/// <summary>
/// Increments the patch segment of a template version string (for example 1.0.1 → 1.0.2).
/// </summary>
public static class TemplateVersionPolicy
{
    public static string IncrementPatch(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "1.0.1";

        var parts = version.Split('.');
        if (parts.Length == 0)
            return "1.0.1";
        if (parts.Length == 1)
            return $"{parts[0]}.0.1";
        if (parts.Length == 2)
            return $"{parts[0]}.{parts[1]}.1";

        if (int.TryParse(parts[2], out var patchVersion))
        {
            patchVersion++;
            return $"{parts[0]}.{parts[1]}.{patchVersion}";
        }

        return $"{parts[0]}.{parts[1]}.1";
    }
}
