using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Support;

public static class AuthenticationInterceptor
{
    private static readonly ConditionalWeakTable<IBrowserContext, AuthUser> IdentityByContext = new();

    public static void SetContextAuthUser(IBrowserContext context, AuthUser user)
    {
        IdentityByContext.AddOrUpdate(context, user);
    }

    public static async Task RegisterAuthenticationAsync(IBrowserContext context, ServiceConfig config)
    {
        SetContextAuthUser(context, AuthUsers.GetDefaultAuthUser());

        await context.RouteAsync($"{config.Url}/**", async route =>
        {
            var user = IdentityByContext.TryGetValue(context, out var value) ? value : AuthUsers.GetDefaultAuthUser();
            var headers = new Dictionary<string, string>(route.Request.Headers)
            {
                ["x-service-email"] = user.Email,
                ["x-service-api-key"] = user.ApiKey,
                ["X-Tenant-ID"] = config.TenantId,
            };

            await route.ContinueAsync(new RouteContinueOptions { Headers = headers });
        });
    }
}
