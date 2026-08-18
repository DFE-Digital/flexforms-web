namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// User-facing copy for Duplicate Tenant. Keep these strings identical to the previous PageModel.
/// </summary>
public static class DuplicateTenantMessages
{
    public const string TenantContextMissing = "Tenant context is not available for this request.";

    public const string ServiceEmailRequired = "Service email is required.";

    public const string ServiceApiKeyRequired = "Enter an ApiKey of at least 32 characters.";

    public const string TenantIdRequired = "Enter a valid tenant id.";

    public const string TenantIdMustDiffer = "New tenant id must be different from the current tenant.";

    public const string CloneFailed = "Could not duplicate tenant.";

    public const string CloneBlocked =
        "Clone was blocked with HTTP 403 (HTML response). "
        + "This usually means Front Door / WAF rejected the request before the API. "
        + "Check WAF logs for POST /v1/admin/tenants/.../clone.";

    public static string CloneFailedHttp(int statusCode) =>
        $"{CloneFailed} (HTTP {statusCode})";

    public static string Created(
        string newTenantName,
        Guid newTenantId,
        int settingsCopied,
        string hostname) =>
        $"Created tenant '{newTenantName}' ({newTenantId}). "
        + $"Copied {settingsCopied} setting(s). Hostname: {hostname}. "
        + "Authorization and InternalServiceAuth secrets (SecretKey + service ApiKeys) were applied. "
        + "Create a template for this tenant before users can access the dashboard.";
}
