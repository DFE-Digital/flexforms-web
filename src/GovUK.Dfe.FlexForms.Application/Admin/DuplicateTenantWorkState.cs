namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the Duplicate Tenant admin page.
/// </summary>
public sealed class DuplicateTenantWorkState
{
    public Guid SourceTenantId { get; set; }

    public string SourceTenantName { get; set; } = string.Empty;

    public Guid NewTenantId { get; set; }

    public string NewTenantName { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string Hostname { get; set; } = string.Empty;

    public string FrontendOrigin { get; set; } = string.Empty;

    public string AuthorizationApiSecretKey { get; set; } = string.Empty;

    public string InternalServiceAuthSecretKey { get; set; } = string.Empty;

    public List<DuplicateTenantServiceSecret> InternalServiceAuthServiceApiKeys { get; set; } = [];

    public bool HasError { get; set; }

    public string? ErrorMessage { get; set; }
}

/// <summary>
/// One InternalServiceAuth Services[] ApiKey row on the Duplicate Tenant form.
/// </summary>
public sealed class DuplicateTenantServiceSecret
{
    public string Email { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
