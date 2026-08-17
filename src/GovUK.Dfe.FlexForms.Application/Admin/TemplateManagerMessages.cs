namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// User-facing copy for Template Manager. Keep these strings identical to the previous PageModel.
/// </summary>
public static class TemplateManagerMessages
{
    public const string SelectTemplate = "Select a template.";

    public const string SelectTenantTemplate = "Select a template for this tenant.";

    public const string SelectVersion = "Select a template version.";

    public const string VersionRequired = "Version number is required";

    public const string SchemaRequired = "JSON schema is required";

    public const string AcknowledgeReportingImpact =
        "You must confirm that you understand the reporting impact before saving.";

    public const string GrantRequiresTemplate = "Select a template before granting access to all users.";

    public const string GrantFailed = "Failed to grant this template to all users in the tenant.";

    public const string LoadFailed = "There was an error loading the template data.";

    public const string ClearFailed = "Failed to clear sessions and caches.";

    public static string GrantedSummary(int granted, int alreadyHad, int total) =>
        $"Granted to {granted} user(s). {alreadyHad} already had access. Total tenant users checked: {total}.";
}
