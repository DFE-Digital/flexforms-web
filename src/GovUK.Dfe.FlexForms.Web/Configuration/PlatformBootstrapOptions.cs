namespace GovUK.Dfe.FlexForms.Web.Configuration;

/// <summary>
/// Bootstrap settings used to call platform API endpoints (host-config and tenant-config)
/// before any tenant is known from the incoming request.
/// </summary>
public sealed class PlatformBootstrapOptions
{
    public const string SectionName = "PlatformBootstrap";

    /// <summary>When true, loads host config at startup and tenant config per request from the API.</summary>
    public bool Enabled { get; set; }

    /// <summary>Base URL of the external applications API (e.g. https://localhost:7089).</summary>
    public string? ApiBaseUrl { get; set; }

    /// <summary>Entra scope for platform app-only tokens (e.g. api://{api-app-id}/.default).</summary>
    public string? Scope { get; set; }

    /// <summary>Directory tenant id for client-credentials flow (optional when using managed identity).</summary>
    public string? TenantId { get; set; }

    /// <summary>Client id for client-credentials flow (Web app registration).</summary>
    public string? ClientId { get; set; }

    /// <summary>Client secret for local development client-credentials flow.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Cache duration for tenant configuration loaded from the API.</summary>
    public int TenantConfigurationCacheMinutes { get; set; } = 10;
}
