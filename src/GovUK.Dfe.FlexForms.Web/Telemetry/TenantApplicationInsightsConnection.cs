using Microsoft.Extensions.Configuration;

namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Reads and parses TenantConfig / host Application Insights connection strings.
/// </summary>
internal static class TenantApplicationInsightsConnection
{
    public const string ConfigurationKey = "ApplicationInsights:ConnectionString";

    public static string? FromConfiguration(IConfiguration? configuration)
    {
        var value = configuration?[ConfigurationKey];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
