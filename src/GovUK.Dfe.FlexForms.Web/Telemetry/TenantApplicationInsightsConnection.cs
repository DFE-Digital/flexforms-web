using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Reads and parses TenantConfig / host Application Insights connection strings.
/// </summary>
internal static class TenantApplicationInsightsConnection
{
    public const string ConfigurationKey = "ApplicationInsights:ConnectionString";
    public const string HttpContextItemKey = "FlexForms.TenantApplicationInsightsConnectionString";

    public static string? FromConfiguration(IConfiguration? configuration)
    {
        var value = configuration?[ConfigurationKey];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string? FromHttpContext(HttpContext? context)
    {
        if (context?.Items.TryGetValue(HttpContextItemKey, out var value) == true)
        {
            return value as string;
        }

        return null;
    }

    /// <summary>
    /// Stash the tenant connection string on the request so Application Insights can still
    /// route after tenant middleware returns (request tracking sends on the way out).
    /// </summary>
    public static void BindToRequest(HttpContext context, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var trimmed = connectionString.Trim();
        context.Items[HttpContextItemKey] = trimmed;

        if (TryGetInstrumentationKey(trimmed, out var instrumentationKey)
            && context.Features.Get<RequestTelemetry>() is { } requestTelemetry)
        {
            requestTelemetry.Context.InstrumentationKey = instrumentationKey;
        }
    }

    public static bool TryGetInstrumentationKey(string connectionString, out string instrumentationKey)
    {
        instrumentationKey = string.Empty;
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = part[..separator];
            if (key.Equals("InstrumentationKey", StringComparison.OrdinalIgnoreCase)
                || key.Equals("ikey", StringComparison.OrdinalIgnoreCase))
            {
                instrumentationKey = part[(separator + 1)..].Trim();
                return instrumentationKey.Length > 0;
            }
        }

        return false;
    }
}
