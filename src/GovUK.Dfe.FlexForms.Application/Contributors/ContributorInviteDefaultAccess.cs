namespace GovUK.Dfe.FlexForms.Application.Contributors;

/// <summary>
/// Defaults for the invite-contributor permission checkboxes, based on the template
/// <c>contributorRoleId</c> (same rule as the API when <c>Permissions</c> is omitted).
/// </summary>
public static class ContributorInviteDefaultAccess
{
    /// <summary>System User role — matches flexforms-api <c>RoleConstants.UserRoleId</c>.</summary>
    public static readonly Guid UserRoleId = Guid.Parse("8D572B81-9174-418A-A469-C50426E37E10");

    /// <summary>
    /// True when the template uses User (or no role), so invitees get full application access by default.
    /// Any other configured role defaults to view-only.
    /// </summary>
    public static bool IsFullEditByDefault(string? contributorRoleId)
    {
        if (string.IsNullOrWhiteSpace(contributorRoleId))
            return true;

        if (!Guid.TryParse(contributorRoleId.Trim(), out var roleId) || roleId == Guid.Empty)
            return true;

        return roleId == UserRoleId;
    }
}
