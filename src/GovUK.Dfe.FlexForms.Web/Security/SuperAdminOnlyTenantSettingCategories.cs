namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// TenantConfig categories editable only by SuperAdmin in Tenant Settings.
/// Keep in sync with
/// <c>GovUK.Dfe.FlexForms.Domain.Tenancy.SuperAdminOnlyTenantSettingCategories</c> (API).
/// </summary>
public static class SuperAdminOnlyTenantSettingCategories
{
    public const string ApplicationTemplates = "ApplicationTemplates";
    public const string Template = "Template";
    public const string ConnectionStrings = "ConnectionStrings";
    public const string FileStorage = "FileStorage";
    public const string Email = "Email";
    public const string ApplicationInsights = "ApplicationInsights";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ApplicationTemplates,
        Template,
        ConnectionStrings,
        FileStorage,
        Email,
        ApplicationInsights
    };

    public static bool IsRestricted(string? category) =>
        !string.IsNullOrWhiteSpace(category) && All.Contains(category.Trim());
}
