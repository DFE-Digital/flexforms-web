using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using GovUK.Dfe.FlexForms.Web.Tenancy;

namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Browser snippet that uses the current tenant's Application Insights connection string
/// (TenantConfig overlay via <see cref="ITenantAppConfiguration"/>), not the host singleton.
/// </summary>
public sealed class TenantJavaScriptSnippet(
    ITenantAppConfiguration appConfig,
    IOptions<ApplicationInsightsServiceOptions> serviceOptions,
    IHttpContextAccessor httpContextAccessor,
    JavaScriptEncoder encoder) : IJavaScriptSnippet
{
    public string FullScript
    {
        get
        {
            var connectionString = TenantApplicationInsightsConnection.FromConfiguration(appConfig.Current);
            if (connectionString is null)
            {
                return string.Empty;
            }

            var telemetryConfiguration = new TelemetryConfiguration { ConnectionString = connectionString };
            return new JavaScriptSnippet(
                telemetryConfiguration,
                serviceOptions,
                httpContextAccessor,
                encoder).FullScript;
        }
    }
}
