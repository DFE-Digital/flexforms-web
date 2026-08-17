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
    public const string CanManagePlatformTenantsPolicy = "CanManagePlatformTenants";

    public const string PermissionClaimType = "permission";

    /// <summary>Claim value for tenant-wide template manager custom roles.</summary>
    public const string TemplateManageAnyClaim = "Template:Any:Manage";

    /// <summary>Claim value for tenant-wide application listing (Caseworker / Case Manager).</summary>
    public const string ApplicationAnyReadClaim = "Application:Any:Read";

    public const string CanReadAnyApplicationPolicy = "CanReadAnyApplication";

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
    /// True when the user can list all applications for the active template
    /// (Admin/SuperAdmin, or Application:Any:Read — e.g. Caseworker).
    /// </summary>
    public static bool CanReadAnyApplication(ClaimsPrincipal? user) =>
        IsAdmin(user)
        || HasPermissionClaim(user, ApplicationAnyReadClaim);

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
    /// Organisation settings (non-secret delegated TenantConfig) — Admin or SuperAdmin.
    /// </summary>
    public static bool CanManageOrganisationSettings(ClaimsPrincipal? user) =>
        IsAdmin(user);

    /// <summary>
    /// Event mappings / schema events for the current tenant — Admin or SuperAdmin.
    /// Same audience as organisation settings (delegated safe TenantConfig categories).
    /// </summary>
    public const string CanManageEventMappingsPolicy = "CanManageEventMappings";

    public static bool CanManageEventMappings(ClaimsPrincipal? user) =>
        IsAdmin(user);

    /// <summary>
    /// TenantConfig settings editor for the current tenant — Admin or SuperAdmin.
    /// Platform-wide tools (new tenant, platform tenants) stay SuperAdmin-only.
    /// </summary>
    public static bool CanManageTenantSettings(ClaimsPrincipal? user) =>
        IsAdmin(user);

    /// <summary>
    /// Create tenant / list all platform tenants — SuperAdmin only.
    /// </summary>
    public static bool CanManagePlatformTenants(ClaimsPrincipal? user) =>
        IsSuperAdmin(user);

    /// <summary>
    /// Read-only tenant configuration summary (auth scheme, hostnames) for tenant Admins.
    /// SuperAdmins use <see cref="CanManageTenantSettings"/> for the full editor.
    /// </summary>
    public static bool CanViewTenantConfigurationSummary(ClaimsPrincipal? user) =>
        IsAdmin(user) && !IsSuperAdmin(user);

    /// <summary>
    /// Roles shown in User Manager add/edit.
    /// Always includes <c>User</c> and non-system (custom) roles.
    /// Tenant <c>Admin</c> is included only for SuperAdmin operators
    /// (injected even if the tenant Admin role row is not yet listed).
    /// </summary>
    public static IReadOnlyList<string> GetUserManagerAssignableRoles(
        ClaimsPrincipal? actor,
        IEnumerable<(string Name, bool IsSystem)>? roles)
    {
        var includeTenantAdmin = IsSuperAdmin(actor);
        var names = (roles ?? [])
            .Where(r =>
                string.Equals(r.Name, "User", StringComparison.OrdinalIgnoreCase)
                || (includeTenantAdmin
                    && string.Equals(r.Name, "Admin", StringComparison.OrdinalIgnoreCase))
                || !r.IsSystem)
            .Select(r => r.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!names.Any(n => string.Equals(n, "User", StringComparison.OrdinalIgnoreCase)))
            names.Add("User");

        if (includeTenantAdmin
            && !names.Any(n => string.Equals(n, "Admin", StringComparison.OrdinalIgnoreCase)))
        {
            names.Add("Admin");
        }

        return names
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool HasPermissionClaim(ClaimsPrincipal? user, string permissionClaimValue) =>
        user is not null
        && user.Claims.Any(c =>
            string.Equals(c.Type, PermissionClaimType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.Value, permissionClaimValue, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Matches claims shaped as <c>{resourceType}:{key}:Manage</c> (Any or specific id/email).
    /// </summary>
    public static bool HasNotificationAccess(ClaimsPrincipal? user, string accessType)
    {
        if (IsAdmin(user))
            return true;

        if (user is null || string.IsNullOrWhiteSpace(accessType))
            return false;

        var suffix = $":{accessType}";
        return user.Claims.Any(c =>
            string.Equals(c.Type, PermissionClaimType, StringComparison.OrdinalIgnoreCase)
            && c.Value.StartsWith("Notifications:", StringComparison.OrdinalIgnoreCase)
            && c.Value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

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
