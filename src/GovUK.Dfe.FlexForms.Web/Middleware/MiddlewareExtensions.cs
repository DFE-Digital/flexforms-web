using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Middleware;

[ExcludeFromCodeCoverage]
public static class MiddlewareExtensions
{
    public static IApplicationBuilder UsePermissionsCache(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PermissionsCacheMiddleware>();
    }
} 