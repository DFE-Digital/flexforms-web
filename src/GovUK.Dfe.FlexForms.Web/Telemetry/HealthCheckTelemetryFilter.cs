using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Filters out health check requests from Application Insights telemetry to prevent 
/// health probes from polluting user metrics and request counts.
/// </summary>
public class HealthCheckTelemetryFilter(ITelemetryProcessor next) : ITelemetryProcessor
{
    private static readonly string[] HealthCheckPaths =
    [
        "/health",
        "/healthz",
        "/liveness",
        "/readiness"
    ];

    /// <summary>
    /// Processes telemetry and filters out health check requests.
    /// </summary>
    /// <param name="item">The telemetry item to process.</param>
    public void Process(ITelemetry item)
    {
        // Only filter request telemetry
        if (item is RequestTelemetry request && IsHealthCheckPath(request.Url?.AbsolutePath))
        {
            return; // Don't send this telemetry
        }

        // Continue processing other telemetry
        next.Process(item);
    }

    internal static bool IsHealthCheckPath(string? path) =>
        !string.IsNullOrEmpty(path)
        && HealthCheckPaths.Any(healthPath => path.Equals(healthPath, StringComparison.OrdinalIgnoreCase));
}

