namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// User-facing copy for Tenant Settings. Keep these strings identical to the previous PageModel.
/// </summary>
public static class TenantSettingsMessages
{
    public const string TenantContextMissing = "Tenant context is not available for this request.";

    public const string ValidateRequired = "Category and settings JSON are required to validate.";

    public const string ValidateFailed = "Could not validate setting.";

    public const string CategoryAndJsonRequired = "Category and settings JSON are required.";

    public const string InvalidTarget = "Target must be Shared, Api, or Web.";

    public const string CategoryRequired = "Enter a category name.";

    public const string CategoryTooLong = "Category must not exceed 50 characters.";

    public const string SettingsJsonRequired = "Enter settings JSON.";

    public const string DeleteFailed = "Could not delete setting.";

    public const string UpdateFailed = "Could not update setting.";

    public const string AddFailed = "Could not add setting.";

    public const string ExportFailed = "Could not export configuration.";

    public const string ImportFileRequired = "Select a JSON file to import.";

    public const string ImportEmpty = "The import file contains no settings.";

    public const string ImportInvalidJson = "The file is not valid JSON.";

    public const string ImportFailed = "Could not import configuration.";

    public const string RefreshFailed = "Could not refresh settings.";

    public const string LoadFailed = "Could not load tenant settings.";

    public const string RefreshSuccess = "Tenant configuration cache refreshed.";

    public static string Deleted(string category, string target) =>
        $"Deleted '{category}' ({target}).";

    public static string Updated(string category, string target) =>
        $"Updated '{category}' ({target}).";

    public static string Added(string category, string target) =>
        $"Added '{category}' ({target}).";

    public static string Imported(int appliedCount, int skippedCount) =>
        $"Imported {appliedCount} settings ({skippedCount} secret placeholders skipped).";
}
