namespace GovUK.Dfe.FlexForms.Web.Tenancy;

/// <summary>
/// Per-request tenant context populated from the platform tenant-config API.
/// </summary>
public interface ITenantRequestContext
{
    /// <summary>Resolved tenant id for the current HTTP request, if any.</summary>
    Guid? TenantId { get; set; }

    /// <summary>Resolved tenant display name.</summary>
    string? TenantName { get; set; }

    /// <summary>
    /// Merged tenant configuration (Shared + Web) as flat key-value pairs compatible with <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
    /// </summary>
    IConfiguration? TenantConfiguration { get; set; }
}
