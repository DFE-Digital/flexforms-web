namespace GovUK.Dfe.FlexForms.Web.Services.Tenant;

/// <summary>
/// Raised when the request presents two different usable public hostnames, or an
/// <c>X-Tenant-ID</c> that does not match the hostname-resolved tenant.
/// </summary>
public sealed class TenantHostnameConflictException : InvalidOperationException
{
    public TenantHostnameConflictException(string message) : base(message)
    {
    }
}
