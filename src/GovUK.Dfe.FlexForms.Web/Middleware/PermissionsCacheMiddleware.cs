using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.Middleware;

[ExcludeFromCodeCoverage]
public class PermissionsCacheMiddleware(
    RequestDelegate next,
    IMemoryCache cache,
    IUsersClient usersClient)
{
    public const string PermissionsCacheKeyPrefix = "UserPermissions_";

    public async Task InvokeAsync(HttpContext context)
    {
        if (AuthenticationPathExclusions.ShouldSkip(context.Request.Path))
        {
            await next(context);
            return;
        }

        var logger = context.RequestServices.GetService<ILogger<PermissionsCacheMiddleware>>();
        var user = context.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    var tenantId = context.RequestServices.GetService<ITenantRequestContext>()?.TenantId;
                    await UserPermissionsCache.RefreshAsync(
                        cache,
                        usersClient,
                        user,
                        logger,
                        context.RequestAborted,
                        forceRefresh: true,
                        tenantId: tenantId);
                    UserPermissionsCache.RemovePermissionClaims(user);

                    var transformation = context.RequestServices.GetRequiredService<IClaimsTransformation>();
                    context.User = await transformation.TransformAsync(user);
                }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                {
                    logger?.LogDebug("Permissions cache refresh canceled for aborted request {Path}", context.Request.Path);
                    return;
                }
                catch (ExternalApplicationsException ex) when (ex.StatusCode == 401)
                {
                    context.Response.Redirect("/Logout?reason=auth_failed");
                    return;
                }
                catch (ExternalApplicationsException ex) when (ex.StatusCode == 403 && IsAuthenticationFailure(ex))
                {
                    context.Response.Redirect("/Logout?reason=token_expired");
                    return;
                }
                catch (ExternalApplicationsException ex) when (ex.StatusCode == 403)
                {
                    logger?.LogWarning(ex, "Permission denied fetching permissions for {UserId}", userId);
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
                {
                    context.Response.Redirect("/Logout?reason=token_expired");
                    return;
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    context.Response.Redirect("/Logout?reason=auth_failed");
                    return;
                }
            }
        }

        await next(context);
    }

    private static bool IsAuthenticationFailure(ExternalApplicationsException ex) =>
        ex.Message.Contains("token", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("Not authenticated", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("No user identifier", StringComparison.OrdinalIgnoreCase);
}
