using Microsoft.Extensions.Hosting;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Internal-service header authentication on the public web edge.
/// Off by default in Production/Prod; on by default in Development, Local and Test.
/// Override with <c>InternalServiceAuth:WebEdgeEnabled</c>.
/// </summary>
public static class InternalServiceAuthEnvironmentGate
{
    public const string WebEdgeEnabledKey = "InternalServiceAuth:WebEdgeEnabled";

    public static bool IsAllowed(IHostEnvironment? environment, IConfiguration? configuration)
    {
        var configured = configuration?.GetValue<bool?>(WebEdgeEnabledKey);
        if (TestAuthenticationEnvironmentGate.IsProduction(environment))
            return configured == true;

        return configured ?? true;
    }
}
