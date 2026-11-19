using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Contributors;

/// <summary>
/// Maps a contributor's Application / ApplicationFiles grants for one application
/// to the same labels used on the invite page.
/// </summary>
public static class ContributorAccessLabels
{
    public static IReadOnlyList<string> ForApplication(
        UserDto contributor,
        Guid applicationId,
        string singular)
    {
        ArgumentNullException.ThrowIfNull(contributor);

        var noun = string.IsNullOrWhiteSpace(singular) ? "application" : singular.Trim();
        var permissions = contributor.Authorization?.Permissions?
            .Where(permission =>
                permission.ApplicationId == applicationId
                && permission.ResourceType is ResourceType.Application or ResourceType.ApplicationFiles)
            .ToList()
            ?? [];

        var labels = new List<string>();

        // Contributors always have Application Read; still list it when present so the card matches the invite checkboxes.
        if (Has(permissions, ResourceType.Application, AccessType.Read))
            labels.Add($"View this {noun}");

        if (Has(permissions, ResourceType.Application, AccessType.Write))
            labels.Add($"Change this {noun}");

        if (Has(permissions, ResourceType.ApplicationFiles, AccessType.Read))
            labels.Add("View uploaded files");

        if (Has(permissions, ResourceType.ApplicationFiles, AccessType.Write))
            labels.Add("Upload files");

        if (Has(permissions, ResourceType.ApplicationFiles, AccessType.Delete))
            labels.Add("Delete uploaded files");

        return labels;
    }

    private static bool Has(
        IEnumerable<UserPermissionDto> permissions,
        ResourceType resourceType,
        AccessType accessType) =>
        permissions.Any(permission =>
            permission.ResourceType == resourceType && permission.AccessType == accessType);
}
