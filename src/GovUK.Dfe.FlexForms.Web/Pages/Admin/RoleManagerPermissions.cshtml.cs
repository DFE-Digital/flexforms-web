using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Sets RolePermissions for a custom tenant role (ResourceType × AccessType, ResourceKey = Any).
/// </summary>
[Authorize(Roles = AdminAccessHelper.AuthorizeRoles)]
public sealed class RoleManagerPermissionsModel(
    IRolesClient rolesClient,
    ILogger<RoleManagerPermissionsModel> logger) : PageModel
{
    public const string AnyResourceKey = "Any";

    [BindProperty(SupportsGet = true)]
    public Guid RoleId { get; set; }

    public string RoleName { get; private set; } = string.Empty;

    public bool IsSystemRole { get; private set; }

    /// <summary>
    /// Selected grants encoded as "{ResourceType}|{AccessType}".
    /// </summary>
    [BindProperty]
    public List<string> SelectedGrants { get; set; } = [];

    public IReadOnlyList<(ResourceType ResourceType, AccessType AccessType, string Key)> GrantOptions { get; private set; }
        = BuildGrantOptions();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(cancellationToken);
        return loaded ? Page() : RedirectToPage("/Admin/RoleManager");
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadRoleMetaAsync(cancellationToken))
            return RedirectToPage("/Admin/RoleManager");

        if (IsSystemRole)
        {
            TempData["RoleManagerError"] = "System role permissions cannot be changed.";
            return RedirectToPage("/Admin/RoleManager");
        }

        try
        {
            var grants = (SelectedGrants ?? [])
                .Select(ParseGrantKey)
                .Where(g => g is not null)
                .Select(g => g!)
                .Select(g => new RolePermissionGrantDto
                {
                    ResourceType = g.Value.ResourceType,
                    ResourceKey = AnyResourceKey,
                    AccessType = g.Value.AccessType
                })
                .ToList();

            await rolesClient.SetPermissionsAsync(
                RoleId,
                new SetRolePermissionsRequest { Permissions = grants },
                cancellationToken);

            TempData["RoleManagerSuccess"] = $"Permissions updated for '{RoleName}'.";
            return RedirectToPage("/Admin/RoleManager");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set permissions for role {RoleId}", RoleId);
            ModelState.AddModelError(string.Empty, RoleManagerModel.GetErrorMessage(ex, "Could not save permissions."));
            GrantOptions = BuildGrantOptions();
            return Page();
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        if (!await LoadRoleMetaAsync(cancellationToken))
            return false;

        if (IsSystemRole)
        {
            TempData["RoleManagerError"] = "System role permissions cannot be changed. Create a custom role instead.";
            return false;
        }

        try
        {
            var existing = await rolesClient.GetPermissionsAsync(RoleId, cancellationToken);
            SelectedGrants = existing?
                .Where(p => string.Equals(p.ResourceKey, AnyResourceKey, StringComparison.OrdinalIgnoreCase))
                .Select(p => EncodeGrantKey(p.ResourceType, p.AccessType))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];

            GrantOptions = BuildGrantOptions();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load permissions for role {RoleId}", RoleId);
            TempData["RoleManagerError"] = RoleManagerModel.GetErrorMessage(ex, "Could not load role permissions.");
            return false;
        }
    }

    private async Task<bool> LoadRoleMetaAsync(CancellationToken cancellationToken)
    {
        try
        {
            var roles = await rolesClient.ListAsync(cancellationToken);
            var role = roles?.FirstOrDefault(r => r.RoleId == RoleId);
            if (role is null)
            {
                TempData["RoleManagerError"] = "Role not found.";
                return false;
            }

            RoleName = role.Name;
            IsSystemRole = role.IsSystem;
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load role {RoleId}", RoleId);
            TempData["RoleManagerError"] = RoleManagerModel.GetErrorMessage(ex, "Could not load role.");
            return false;
        }
    }

    public static string EncodeGrantKey(ResourceType resourceType, AccessType accessType) =>
        $"{resourceType}|{accessType}";

    private static (ResourceType ResourceType, AccessType AccessType)? ParseGrantKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var parts = key.Split('|', 2);
        if (parts.Length != 2)
            return null;

        if (!Enum.TryParse<ResourceType>(parts[0], ignoreCase: true, out var resourceType))
            return null;

        if (!Enum.TryParse<AccessType>(parts[1], ignoreCase: true, out var accessType))
            return null;

        return (resourceType, accessType);
    }

    private static IReadOnlyList<(ResourceType ResourceType, AccessType AccessType, string Key)> BuildGrantOptions()
    {
        var options = new List<(ResourceType, AccessType, string)>();
        foreach (ResourceType resourceType in Enum.GetValues<ResourceType>())
        {
            foreach (AccessType accessType in Enum.GetValues<AccessType>())
            {
                options.Add((resourceType, accessType, EncodeGrantKey(resourceType, accessType)));
            }
        }

        return options;
    }
}
