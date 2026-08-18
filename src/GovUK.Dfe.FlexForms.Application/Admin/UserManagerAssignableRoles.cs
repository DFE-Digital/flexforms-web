namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Roles shown in User Manager add/edit. Always includes <c>User</c> and non-system roles.
/// Tenant <c>Admin</c> is included only when <paramref name="includeTenantAdmin"/> is true.
/// </summary>
public static class UserManagerAssignableRoles
{
    public static IReadOnlyList<string> Resolve(
        IEnumerable<(string Name, bool IsSystem)>? roles,
        bool includeTenantAdmin)
    {
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
}
