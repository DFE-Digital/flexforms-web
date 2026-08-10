using Microsoft.Extensions.Hosting;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Hard rule: Test Authentication must never be active in Production,
/// regardless of tenant <c>Authentication:Scheme</c> or <c>TestAuthentication:Enabled</c>.
/// </summary>
public static class TestAuthenticationEnvironmentGate
{
    /// <summary>
    /// Returns <c>true</c> when Test Authentication may be used in this environment.
    /// </summary>
    public static bool IsAllowed(IHostEnvironment? environment)
        => environment is null || IsAllowed(environment.EnvironmentName);

    /// <summary>
    /// Returns <c>true</c> when Test Authentication may be used for the given environment name.
    /// Blocks <c>Production</c> and the legacy alias <c>Prod</c>.
    /// </summary>
    public static bool IsAllowed(string? environmentName)
    {
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            return true;
        }

        return !string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(environmentName, "Prod", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns <c>true</c> when the host environment is Production (or Prod).
    /// </summary>
    public static bool IsProduction(IHostEnvironment? environment)
        => !IsAllowed(environment);
}
