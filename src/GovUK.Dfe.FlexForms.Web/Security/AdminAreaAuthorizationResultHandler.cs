using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// For authenticated users who lack admin access, treat <c>/admin</c> as not found
/// instead of forbidding (which previously crashed the process when the forbid scheme
/// was the remote IdP). Unauthenticated users still get a normal challenge/login.
/// </summary>
public sealed class AdminAreaAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && context.User.Identity?.IsAuthenticated == true
            && IsAdminPath(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        return _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    public static bool IsAdminPath(PathString path) =>
        path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase);
}
