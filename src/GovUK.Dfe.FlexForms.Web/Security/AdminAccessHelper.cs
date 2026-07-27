using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Shared admin-role checks. Accepts both current SuperAdmin and legacy Admin claims.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AdminAccessHelper
{
    public const string AuthorizeRoles = "Admin,SuperAdmin";

    public static bool IsAdmin(ClaimsPrincipal? user) =>
        user is not null
        && (user.IsInRole("SuperAdmin") || user.IsInRole("Admin"));
}
