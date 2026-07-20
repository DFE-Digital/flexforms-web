using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Evaluates application write permissions from the user's permission claims.
/// Mirrors API write-access semantics: Admin role or an explicit Application write claim.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ApplicationPermissionHelper
{
    private const string PermissionClaimType = "permission";

    /// <summary>
    /// Returns true when the user can write the specified application.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <param name="applicationId">The application identifier.</param>
    public static bool CanWriteApplication(ClaimsPrincipal? user, Guid applicationId)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole("Admin"))
        {
            return true;
        }

        var expected = $"Application:{applicationId}:Write";
        return user.Claims.Any(c =>
            c.Type == PermissionClaimType
            && string.Equals(c.Value, expected, StringComparison.OrdinalIgnoreCase));
    }
}
