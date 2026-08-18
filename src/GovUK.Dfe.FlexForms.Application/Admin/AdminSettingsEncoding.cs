using System.Text;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Encodes TenantConfig JSON as Base64 for the API (WAF-safe; mirrors template schema transport).
/// </summary>
public static class AdminSettingsEncoding
{
    public static string ToBase64(string settingsJson) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(settingsJson));
}
