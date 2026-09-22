using Microsoft.Extensions.Hosting;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Mirrors the API gate: SuperAdmin may see plaintext tenant secrets in Dev/Test only.
/// Production and unknown environments never allow plaintext in the UI copy/expectations.
/// </summary>
public static class TenantSettingSecretPlaintextGate
{
    private static readonly HashSet<string> PlaintextEnvironments = new(StringComparer.OrdinalIgnoreCase)
    {
        Environments.Development,
        "Local",
        "Dev",
        "Test",
        "Testing"
    };

    public static bool IsPlaintextEnvironment(IHostEnvironment? environment)
        => IsPlaintextEnvironment(environment?.EnvironmentName);

    public static bool IsPlaintextEnvironment(string? environmentName)
    {
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            return false;
        }

        if (string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Prod", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return PlaintextEnvironments.Contains(environmentName.Trim());
    }

    public static bool AllowsSuperAdminPlaintext(IHostEnvironment? environment, bool isInteractiveSuperAdmin)
        => isInteractiveSuperAdmin && IsPlaintextEnvironment(environment);
}
