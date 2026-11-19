namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// User-facing copy for the New tenant page.
/// </summary>
public static class NewTenantMessages
{
    public const string TenantContextMissing = "Tenant context is not available for this request.";

    public const string TenantIdRequired = "Enter a valid tenant id.";

    public const string TenantIdMustDiffer = "New tenant id must be different from the current tenant.";

    public const string SettingsRequired = "Enter at least one setting.";

    public const string ServiceNameRequired = "Enter a service name";

    public const string ServiceNameTooLong = "Service name must be 200 characters or fewer";

    public const string SettingsLoadFailed =
        "Could not load the current tenant's settings. Fill in each setting before creating the tenant.";

    public const string CreateFailed = "Could not create tenant.";

    public const string CreateBlocked =
        "Create was blocked with HTTP 403 (HTML response). "
        + "This usually means Front Door / WAF rejected the request before the API. "
        + "Check WAF logs for POST /v1/admin/tenants/.../clone.";

    public static string InvalidJson(string category, string target) =>
        $"{category} ({target}) must be valid JSON, or left blank to leave it out.";

    public static string Created(
        string newTenantName,
        Guid newTenantId,
        int settingsCreated,
        string hostname) =>
        $"Created tenant '{newTenantName}' ({newTenantId}) with {settingsCreated} setting(s). Hostname: {hostname}. "
        + "Create a template and add users before using the new tenant.";

    /// <summary>Why the New tenant page asks for a category.</summary>
    public static string Purpose(string category, bool isOptional = false) => isOptional
        ? CopiedPurpose(category)
        : category switch
    {
        "ConnectionStrings" => "DefaultConnection for the tenant's application database. Every API request needs it.",
        "Authorization" => "Signs the tenant's user tokens. New values were generated for this tenant.",
        "AzureAd" => "Lets Web's service identity call the API for this tenant (token exchange). Needs TenantId and ClientId (or Audience).",
        "Authentication" => "Which sign-in the tenant uses. Outside Production new tenants start on TestAuthentication; change Scheme to DfESignIn or EntraSso when the real client is ready.",
        "DfESignIn" => "DfE Sign-in client, copied from the current tenant. Web derives token refresh settings from it whatever the scheme. To sign in with it, the API needs DiscoveryEndpoint and ClientId.",
        "EntraSso" => "Microsoft Entra sign-in client. The API needs Enabled true, TenantId and ClientId to validate sign-ins.",
        "TestAuthentication" => "Test sign-in, enabled by default. A new signing key, issuer and audience were generated for this tenant; Api and Web must match. Never offered in Production.",
        "TokenRefresh" => "Token refresh endpoints and client. Web checks these on every request.",
        "Email" => "GOV.UK Notify, needed to email the Test sign-in one-time password.",
        "EmailTemplates" => "Fill in TestAuthPasswordEmail with the GOV.UK Notify template id for the Test sign-in one-time password.",
        _ => string.Empty
    };

    private static string CopiedPurpose(string category) => category switch
    {
        "Layout" =>
            "Other layout options copied from the current tenant. ServiceName is always set from the Service name field above.",
        "ApplicationTemplates" =>
            "Template ids the current tenant owns are rejected, because a tenant can only map its own templates. "
            + "Clear them (and use the new hostname as the key) unless they are shared legacy templates. "
            + "If the new tenant uses a different database, leave mappings out and add them after creation.",
        "FileStorage" =>
            "Keeping __REDACTED__ reuses the current tenant's storage account and share.",
        "Email" =>
            "Keeping __REDACTED__ reuses the current tenant's GOV.UK Notify API key.",
        "ExternalApplicationsApiClient" =>
            "Overrides Web's API client settings for this tenant. Keeping __REDACTED__ reuses the current tenant's credentials.",
        "EntraSso" or "DfESignIn" =>
            "Sign-in client for a scheme this tenant does not use yet. Keeping __REDACTED__ reuses the current tenant's client secret.",
        _ => "Copied from the current tenant."
    };
}
