namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Async-local Application Insights connection string for the current tenant request.
/// Telemetry may flush after the HTTP context is gone, so this is not stored on HttpContext.
/// </summary>
public static class TenantApplicationInsightsScope
{
    private static readonly AsyncLocal<string?> ConnectionString = new();

    public static string? CurrentConnectionString => ConnectionString.Value;

    public static IDisposable Begin(string? connectionString)
    {
        var previous = ConnectionString.Value;
        ConnectionString.Value = string.IsNullOrWhiteSpace(connectionString) ? null : connectionString.Trim();
        return new Reset(previous);
    }

    private sealed class Reset(string? previous) : IDisposable
    {
        public void Dispose() => ConnectionString.Value = previous;
    }
}
