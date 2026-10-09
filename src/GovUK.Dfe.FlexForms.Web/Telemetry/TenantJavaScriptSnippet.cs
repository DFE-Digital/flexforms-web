using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;
using NetEscapades.AspNetCore.SecurityHeaders;
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
            if (connectionString is null
                || !TenantApplicationInsightsConnection.TryGetInstrumentationKey(connectionString, out _))
            {
                return string.Empty;
            }

            string script;
            try
            {
                var telemetryConfiguration = new TelemetryConfiguration { ConnectionString = connectionString };
                script = new JavaScriptSnippet(
                    telemetryConfiguration,
                    serviceOptions,
                    httpContextAccessor,
                    encoder).FullScript;
            }
            catch (ArgumentException)
            {
                // A malformed tenant connection string must not break page rendering.
                return string.Empty;
            }

            var nonce = httpContextAccessor.HttpContext?.GetNonce();
            return string.IsNullOrEmpty(nonce)
                ? script
                : script.Replace("<script", $"<script nonce=\"{nonce}\"", StringComparison.OrdinalIgnoreCase);
        }
    }
}
