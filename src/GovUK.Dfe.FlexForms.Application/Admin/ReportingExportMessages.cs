namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// User-facing copy for the Reporting export admin page.
/// </summary>
public static class ReportingExportMessages
{
    public const string TenantContextMissing = "We could not work out which tenant you are managing. Sign in again and retry.";

    public const string LoadDefaultFailed = "Could not load the reporting export settings.";

    public const string LoadPolicyFailed = "Could not load the template's fields.";

    public const string LoadRefreshFailed = "Could not check the progress of the reporting refresh.";

    public const string SaveFailed = "Could not save the reporting export change.";

    public const string SelectTemplate = "Select a template.";

    public const string SelectFields = "Select at least one field.";

    public const string ReasonRequiredForExportAll =
        "Explain why every new field can be exported without approval, for example the data protection agreement that covers it.";

    public const string Saved = "Your change has been saved. Reporting data is being refreshed to match it.";

    public const string Unchanged = "Nothing changed: the settings were already as you chose.";
}
