using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Builds OIDC public callback URLs from the current request host (tenant origin).
/// </summary>
internal static class DfESignInOidcPublicUrls
{
    /// <summary>
    /// Sets <c>post_logout_redirect_uri</c> to the signed-out callback on the
    /// current request origin so multi-tenant hosts (e.g. rgvisits.localhost)
    /// are not redirected to the bootstrap Transfers host (localhost).
    /// </summary>
    public static void ApplyPostLogoutRedirectUri(
        RedirectContext context,
        string? signedOutCallbackPath = null)
    {
        var path = signedOutCallbackPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = context.Options.SignedOutCallbackPath.HasValue
                ? context.Options.SignedOutCallbackPath.Value
                : "/signout-callback-oidc";
        }

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        context.ProtocolMessage.PostLogoutRedirectUri = BuildAbsoluteUrl(context.HttpContext, path);
    }

    /// <summary>
    /// Builds an absolute URL on the current request origin (scheme + host + port).
    /// </summary>
    public static string BuildAbsoluteUrl(HttpContext httpContext, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }
        else if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        var request = httpContext.Request;
        var port = request.Host.Port ?? -1;
        var builder = new UriBuilder(request.Scheme, request.Host.Host, port, path);
        return builder.Uri.AbsoluteUri;
    }
}
