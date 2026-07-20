namespace GovUK.Dfe.FlexForms.Web.Configuration;

/// <summary>API response for <c>GET /v1/host-config</c>.</summary>
public sealed record PlatformHostConfigurationResponse(
    string Target,
    DateTime LoadedAtUtc,
    IReadOnlyDictionary<string, string?> Configuration);

/// <summary>API response for <c>GET /v1/tenant-config/tenants/{id}</c>.</summary>
public sealed record PlatformTenantConfigurationResponse(
    Guid TenantId,
    string TenantName,
    string Target,
    DateTime LoadedAtUtc,
    IReadOnlyDictionary<string, string?> Configuration);

/// <summary>API response for <c>GET /v1/tenant-config/resolve</c>.</summary>
public sealed record PlatformTenantResolutionResponse(
    Guid TenantId,
    string TenantName,
    string Hostname);
