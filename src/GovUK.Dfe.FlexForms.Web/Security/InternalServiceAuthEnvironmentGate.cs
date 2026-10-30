using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.Extensions.Hosting;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Internal-service header authentication on the public web edge.
/// Off by default in Production/Prod; on by default in Development, Local and Test.
/// Tenant <c>InternalServiceAuth:WebEdgeEnabled</c> overrides the environment default.
/// Host <c>InternalServiceAuth:WebEdgeEnabled=false</c> is a platform kill-switch.
/// </summary>
public static class InternalServiceAuthEnvironmentGate
{
    public const string WebEdgeEnabledKey = "InternalServiceAuth:WebEdgeEnabled";

    public static bool IsAllowed(IHostEnvironment? environment, IConfiguration? hostConfiguration)
        => IsAllowed(environment, hostConfiguration, tenantConfiguration: null);

    public static bool IsAllowedForRequest(
        HttpContext? httpContext,
        IHostEnvironment? environment,
        IConfiguration? hostConfiguration)
    {
        var tenantConfiguration = httpContext?.RequestServices
            .GetService<ITenantRequestContext>()
            ?.TenantConfiguration;
        return IsAllowed(environment, hostConfiguration, tenantConfiguration);
    }

    public static bool IsAllowed(
        IHostEnvironment? environment,
        IConfiguration? hostConfiguration,
        IConfiguration? tenantConfiguration)
    {
        var hostFlag = hostConfiguration?.GetValue<bool?>(WebEdgeEnabledKey);
        if (hostFlag == false)
            return false;

        var tenantFlag = tenantConfiguration?.GetValue<bool?>(WebEdgeEnabledKey);
        if (tenantFlag.HasValue)
            return tenantFlag.Value;

        if (hostFlag == true)
            return true;

        return !TestAuthenticationEnvironmentGate.IsProduction(environment);
    }
}
