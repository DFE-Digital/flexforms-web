using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Shared admin-role and Manage-access checks for navigation and page authorization.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AdminAccessHelper
{
    public const string AuthorizeRoles = "Admin,SuperAdmin";

    public const string CanAccessAdminAreaPolicy = "CanAccessAdminArea";
    public const string CanManageTemplatesPolicy = "CanManageTemplates";
    public const string CanManageUsersPolicy = "CanManageUsers";
    public const string CanManageTenantSettingsPolicy = "CanManageTenantSettings";

    public const string PermissionClaimType = "permission";

    /// <summary>Claim value for tenant-wide template manager custom roles.</summary>
    public const string TemplateManageAnyClaim = "Template:Any:Manage";

    /// <summary>Claim value for tenant-wide user manager custom roles.</summary>
    public const string UserManageAnyClaim = "User:Any:Manage";

    /// <summary>True for SuperAdmin only.</summary>
    public static bool IsSuperAdmin(ClaimsPrincipal? user) =>
        user is not null && user.IsInRole("SuperAdmin");

    /// <summary>True for SuperAdmin or tenant Admin.</summary>
    public static bool IsAdmin(ClaimsPrincipal? user) =>
        user is not null
        && (user.IsInRole("SuperAdmin") || user.IsInRole("Admin"));

    /// <summary>
    /// True when the user can open the Admin area (nav + /admin hub):
    /// SuperAdmin/Admin, or any Template/User Manage claim.
    /// </summary>
    public static bool CanAccessAdminArea(ClaimsPrincipal? user) =>
        IsAdmin(user)
        || HasAnyManageClaim(user, "Template")
        || HasAnyManageClaim(user, "User");

    /// <summary>
    /// True when the user is Admin/SuperAdmin or has <see cref="TemplateManageAnyClaim"/>
    /// (or any other Template:*:Manage claim).
    /// </summary>
    public static bool CanManageTemplates(ClaimsPrincipal? user) =>
        IsAdmin(user)
        || HasAnyManageClaim(user, "Template");

    /// <summary>
    /// True when the user is Admin/SuperAdmin or has any User:*:Manage claim.
    /// </summary>
    public static bool CanManageUsers(ClaimsPrincipal? user) =>
        IsAdmin(user)
        || HasAnyManageClaim(user, "User");

    /// <summary>
    /// Role Manager stays Admin/SuperAdmin only (no Role ResourceType Manage grants).
    /// </summary>
    public static bool CanManageRoles(ClaimsPrincipal? user) =>
        IsAdmin(user);

    /// <summary>
    /// TenantConfig settings editor — SuperAdmin only (decrypted secrets).
    /// </summary>
    public static bool CanManageTenantSettings(ClaimsPrincipal? user) =>
        IsSuperAdmin(user);

    public static bool HasPermissionClaim(ClaimsPrincipal? user, string permissionClaimValue) =>
        user is not null
        && user.Claims.Any(c =>
            string.Equals(c.Type, PermissionClaimType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.Value, permissionClaimValue, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Matches claims shaped as <c>{resourceType}:{key}:Manage</c> (Any or specific id/email).
    /// </summary>
    public static bool HasAnyManageClaim(ClaimsPrincipal? user, string resourceType)
    {
        if (user is null || string.IsNullOrWhiteSpace(resourceType))
            return false;

        var prefix = $"{resourceType}:";
        const string suffix = ":Manage";

        return user.Claims.Any(c =>
            string.Equals(c.Type, PermissionClaimType, StringComparison.OrdinalIgnoreCase)
            && c.Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && c.Value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
