using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Evaluates application write permissions from the user's permission claims.
/// Mirrors API write-access semantics: admin role or an explicit Application write claim.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ApplicationPermissionHelper
{
    private const string PermissionClaimType = "permission";

    /// <summary>
    /// Returns true when the user can write the specified application.
    /// </summary>
    public static bool CanWriteApplication(ClaimsPrincipal? user, Guid applicationId)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        if (AdminAccessHelper.IsAdmin(user))
            return true;

        var expected = $"Application:{applicationId}:Write";
        return user.Claims.Any(c =>
            c.Type == PermissionClaimType
            && string.Equals(c.Value, expected, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns true when the user can read the specified application
    /// (exact id, tenant-wide Any, or admin).
    /// </summary>
    public static bool CanReadApplication(ClaimsPrincipal? user, Guid applicationId)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        if (AdminAccessHelper.IsAdmin(user))
            return true;

        return HasPermission(user, $"Application:{applicationId}:Read")
            || HasPermission(user, "Application:Any:Read");
    }

    private static bool HasPermission(ClaimsPrincipal user, string expected) =>
        user.Claims.Any(c =>
            c.Type == PermissionClaimType
            && string.Equals(c.Value, expected, StringComparison.OrdinalIgnoreCase));
}
