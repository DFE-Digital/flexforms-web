namespace GovUK.Dfe.FlexForms.Web.Tenancy;

/// <inheritdoc />
public sealed class TenantRequestContext : ITenantRequestContext
{
    public Guid? TenantId { get; set; }

    public string? TenantName { get; set; }

    public IConfiguration? TenantConfiguration { get; set; }
}
