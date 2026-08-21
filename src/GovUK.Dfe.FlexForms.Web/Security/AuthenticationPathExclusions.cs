namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Paths that must not trigger permission loading, status-code rewrites,
/// or other authenticated middleware side effects.
/// </summary>
internal static class AuthenticationPathExclusions
{
    private static readonly string[] Paths =
    [
        "/signin-oidc",
        "/signout-callback-oidc",
        "/signin-entra",
        "/signout-callback-entra",
        "/Logout",
        "/TestLogin",
        "/TestLogout",
        "/health",
        "/healthz",
        "/liveness",
        "/readiness",
        "/css",
        "/js",
        "/lib",
        "/assets",
        "/images",
        "/fonts",
        "/favicon.ico"
    ];

    private static readonly string[] StaticExtensions =
    [
        ".js",
        ".css",
        ".map",
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".svg",
        ".ico",
        ".woff",
        ".woff2",
        ".ttf",
        ".eot",
        ".webp"
    ];

    /// <summary>
    /// Returns true when the request path is an auth callback, health probe, or static asset.
    /// </summary>
    public static bool ShouldSkip(PathString path)
    {
        if (!path.HasValue)
        {
            return false;
        }

        var pathValue = path.Value!;

        foreach (var excluded in Paths)
        {
            if (pathValue.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var extension in StaticExtensions)
        {
            if (pathValue.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
