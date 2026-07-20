namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Paths used by OIDC sign-in/sign-out that must not trigger permission loading,
/// status-code rewrites, or other authenticated middleware side effects.
/// </summary>
internal static class AuthenticationPathExclusions
{
    private static readonly string[] Paths =
    [
        "/signin-oidc",
        "/signout-callback-oidc",
        "/signin-entra",
        "/signout-callback-entra",
        "/Logout"
    ];

    /// <summary>
    /// Returns true when the request path is an authentication callback or logout endpoint.
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

        return false;
    }
}
