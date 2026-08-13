using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Sets RolePermissions for a custom tenant role (ResourceType + ResourceKey + AccessType).
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
    /// Selected grants encoded as "{ResourceType}|{ResourceKey}|{AccessType}".
    /// </summary>
    [BindProperty]
    public List<string> SelectedGrants { get; set; } = [];

    [BindProperty]
    public ResourceType NewResourceType { get; set; } = ResourceType.Application;

    [BindProperty]
    public string? NewResourceKey { get; set; }

    [BindProperty]
    public AccessType NewAccessType { get; set; } = AccessType.Read;

    public IReadOnlyList<ResourceType> ResourceTypes { get; } = Enum.GetValues<ResourceType>().ToArray();

    public IReadOnlyList<AccessType> AccessTypes { get; } = Enum.GetValues<AccessType>().ToArray();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(cancellationToken);
        return loaded ? Page() : RedirectToPage("/Admin/RoleManager");
    }

    public async Task<IActionResult> OnPostAddAsync(CancellationToken cancellationToken)
    {
        if (!await LoadRoleMetaAsync(cancellationToken))
            return RedirectToPage("/Admin/RoleManager");

        if (IsSystemRole)
        {
            TempData["RoleManagerError"] = "System role permissions cannot be changed.";
            return RedirectToPage("/Admin/RoleManager");
        }

        SelectedGrants = NormalizeGrants(SelectedGrants);

        var resourceKey = NewResourceKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            ModelState.AddModelError(nameof(NewResourceKey), "Enter a resource key.");
            return Page();
        }

        var validationError = ValidateGrant(NewResourceType, resourceKey, NewAccessType);
        if (validationError is not null)
        {
            ModelState.AddModelError(nameof(NewResourceKey), validationError);
            return Page();
        }

        var key = EncodeGrantKey(NewResourceType, resourceKey, NewAccessType);
        if (SelectedGrants.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                string.Empty,
                $"{NewResourceType} / {resourceKey} / {NewAccessType} is already in the list.");
            return Page();
        }

        SelectedGrants.Add(key);
        SelectedGrants = NormalizeGrants(SelectedGrants);

        return await SaveAndReloadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRemoveAsync(string grantKey, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(NewResourceKey));
        ModelState.Remove(nameof(NewResourceType));
        ModelState.Remove(nameof(NewAccessType));

        if (!await LoadRoleMetaAsync(cancellationToken))
            return RedirectToPage("/Admin/RoleManager");

        if (IsSystemRole)
        {
            TempData["RoleManagerError"] = "System role permissions cannot be changed.";
            return RedirectToPage("/Admin/RoleManager");
        }

        SelectedGrants = NormalizeGrants(SelectedGrants);
        SelectedGrants.RemoveAll(g => string.Equals(g, grantKey, StringComparison.OrdinalIgnoreCase));

        return await SaveAndReloadAsync(cancellationToken);
    }

    private async Task<IActionResult> SaveAndReloadAsync(CancellationToken cancellationToken)
    {
        foreach (var grant in SelectedGrants.Select(ParseGrantKey).Where(g => g is not null))
        {
            var error = ValidateGrant(grant!.Value.ResourceType, grant.Value.ResourceKey, grant.Value.AccessType);
            if (error is not null)
            {
                ModelState.AddModelError(string.Empty, error);
                return Page();
            }
        }

        try
        {
            var grants = SelectedGrants
                .Select(ParseGrantKey)
                .Where(g => g is not null)
                .Select(g => g!)
                .Select(g => new RolePermissionGrantDto
                {
                    ResourceType = g.Value.ResourceType,
                    ResourceKey = g.Value.ResourceKey,
                    AccessType = g.Value.AccessType
                })
                .ToList();

            await rolesClient.SetPermissionsAsync(
                RoleId,
                new SetRolePermissionsRequest { Permissions = grants },
                cancellationToken);

            NewResourceKey = string.Empty;
            await LoadPermissionsAsync(cancellationToken);
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set permissions for role {RoleId}", RoleId);
            ModelState.AddModelError(string.Empty, RoleManagerModel.GetErrorMessage(ex, "Could not save permissions."));
            await LoadPermissionsAsync(cancellationToken);
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
            await LoadPermissionsAsync(cancellationToken);
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

    private async Task LoadPermissionsAsync(CancellationToken cancellationToken)
    {
        var existing = await rolesClient.GetPermissionsAsync(RoleId, cancellationToken);
        SelectedGrants = NormalizeGrants(
            existing?
                .Select(p => EncodeGrantKey(p.ResourceType, p.ResourceKey, p.AccessType))
                .ToList() ?? []);
    }

    public static string EncodeGrantKey(ResourceType resourceType, string resourceKey, AccessType accessType) =>
        $"{resourceType}|{resourceKey.Trim()}|{accessType}";

    public static string FormatGrant(string key)
    {
        var parsed = ParseGrantKey(key);
        return parsed is null
            ? key
            : $"{parsed.Value.ResourceType} / {parsed.Value.ResourceKey} / {parsed.Value.AccessType}";
    }

    /// <summary>
    /// Mirrors API <c>RolePermissionGrantRules</c>.
    /// </summary>
    public static string? ValidateGrant(ResourceType resourceType, string resourceKey, AccessType accessType)
    {
        var key = resourceKey.Trim();
        if (accessType == AccessType.Manage)
        {
            if (resourceType != ResourceType.Template && resourceType != ResourceType.User)
            {
                return "Access type 'Manage' is only allowed for Template or User permissions.";
            }

            if (string.Equals(key, AnyResourceKey, StringComparison.OrdinalIgnoreCase))
                return null;
        }
        else if (string.Equals(key, AnyResourceKey, StringComparison.OrdinalIgnoreCase))
        {
            if ((resourceType == ResourceType.Template && accessType == AccessType.Write)
                || (resourceType == ResourceType.Template && accessType == AccessType.Manage)
                || (resourceType == ResourceType.User && accessType == AccessType.Manage)
                || (resourceType == ResourceType.Application && accessType == AccessType.Read)
                || (resourceType == ResourceType.ApplicationFiles && accessType == AccessType.Read)
                || (resourceType == ResourceType.FileValidation && accessType == AccessType.Write))
            {
                return null;
            }

            return $"Resource key '{AnyResourceKey}' is only allowed for Template — Write, " +
                   "Template — Manage, User — Manage, Application — Read, " +
                   "ApplicationFiles — Read, or FileValidation — Write. " +
                   "For other combinations, use a specific resource id or email.";
        }

        return resourceType switch
        {
            ResourceType.Application or ResourceType.ApplicationFiles or ResourceType.Template
                or ResourceType.File or ResourceType.FileValidation or ResourceType.Task or ResourceType.TaskGroup
                or ResourceType.Page or ResourceType.Field
                when !Guid.TryParse(key, out var id) || id == Guid.Empty
                => $"{resourceType} resource key must be a valid non-empty GUID (the resource id) or 'Any' (where allowed).",

            ResourceType.User or ResourceType.Notifications
                when !key.Contains('@', StringComparison.Ordinal) && !Guid.TryParse(key, out _)
                => $"{resourceType} resource key must be a user email (or a service client id).",

            _ => null
        };
    }

    private static List<string> NormalizeGrants(IEnumerable<string>? grants) =>
        (grants ?? [])
            .Select(ParseGrantKey)
            .Where(g => g is not null)
            .Select(g => EncodeGrantKey(g!.Value.ResourceType, g.Value.ResourceKey, g.Value.AccessType))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static (ResourceType ResourceType, string ResourceKey, AccessType AccessType)? ParseGrantKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var parts = key.Split('|', 3);
        if (parts.Length != 3)
            return null;

        if (!Enum.TryParse<ResourceType>(parts[0], ignoreCase: true, out var resourceType))
            return null;

        if (string.IsNullOrWhiteSpace(parts[1]))
            return null;

        if (!Enum.TryParse<AccessType>(parts[2], ignoreCase: true, out var accessType))
            return null;

        return (resourceType, parts[1].Trim(), accessType);
    }
}
