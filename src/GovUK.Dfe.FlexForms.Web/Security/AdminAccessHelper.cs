using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Shared admin-role and template-management checks.
/// Accepts both current SuperAdmin and legacy Admin claims.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AdminAccessHelper
{
    public const string AuthorizeRoles = "Admin,SuperAdmin";

    public const string CanManageTemplatesPolicy = "CanManageTemplates";

    /// <summary>Claim value for Template Manager custom roles.</summary>
    public const string TemplateManageWriteClaim = "Template:Manage:Write";

    public static bool IsAdmin(ClaimsPrincipal? user) =>
        user is not null
        && (user.IsInRole("SuperAdmin") || user.IsInRole("Admin"));

    /// <summary>
    /// True when the user is Admin/SuperAdmin or has <see cref="TemplateManageWriteClaim"/>.
    /// </summary>
    public static bool CanManageTemplates(ClaimsPrincipal? user) =>
        IsAdmin(user)
        || HasPermissionClaim(user, TemplateManageWriteClaim);

    public static bool HasPermissionClaim(ClaimsPrincipal? user, string permissionClaimValue) =>
        user is not null
        && user.Claims.Any(c =>
            string.Equals(c.Type, "permission", StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.Value, permissionClaimValue, StringComparison.OrdinalIgnoreCase));
}
