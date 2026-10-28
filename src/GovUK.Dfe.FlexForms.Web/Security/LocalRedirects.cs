namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Same rules as <see cref="Microsoft.AspNetCore.Mvc.IUrlHelper.IsLocalUrl"/>:
/// rooted paths only, no protocol-relative URLs.
/// </summary>
public static class LocalRedirects
{
    public const string FallbackPath = "/applications/dashboard";

    public static bool IsLocal(string? url)
    {
        if (string.IsNullOrEmpty(url) || url.Any(char.IsControl))
            return false;

        // "/path" is local; "//evil" and "/\\evil" are not.
        if (url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\')))
            return !url.Contains('\\');

        return url.StartsWith("~/", StringComparison.Ordinal);
    }

    public static string OrFallback(string? url, string fallback = FallbackPath)
        => IsLocal(url) ? url! : fallback;
}
