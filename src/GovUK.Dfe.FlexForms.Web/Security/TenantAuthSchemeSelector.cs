using GovUK.Dfe.FlexForms.Web.Tenancy;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Resolves the interactive IdP for the current request from tenant configuration
/// (platform bootstrap) with host <see cref="EntraSsoOptions"/> as fallback.
/// </summary>
public static class TenantAuthSchemeSelector
{
    /// <summary>
    /// Returns <c>true</c> when Entra SSO should be used for the current request.
    /// Prefers <c>EntraSso:Enabled</c> from the resolved tenant Web config.
    /// </summary>
    public static bool IsEntraSsoEnabled(HttpContext? httpContext, IOptions<EntraSsoOptions>? hostEntraOptions = null)
    {
        if (httpContext is not null)
        {
            var tenantContext = httpContext.RequestServices.GetService<ITenantRequestContext>();
            var tenantFlag = tenantContext?.TenantConfiguration?["EntraSso:Enabled"];
            if (bool.TryParse(tenantFlag, out var tenantEnabled))
            {
                return tenantEnabled;
            }
        }

        return hostEntraOptions?.Value.Enabled ?? false;
    }
}
