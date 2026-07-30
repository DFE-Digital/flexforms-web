using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Sets user-level Permissions for a tenant member (ResourceType + ResourceKey + AccessType).
/// Does not affect permissions inherited from the user's role.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageUsersPolicy)]
public sealed class UserManagerPermissionsModel(
    IUsersClient usersClient,
    ILogger<UserManagerPermissionsModel> logger) : PageModel
{
    public const string AnyResourceKey = RoleManagerPermissionsModel.AnyResourceKey;

    [BindProperty(SupportsGet = true)]
    public Guid UserId { get; set; }

    public string UserName { get; private set; } = string.Empty;

    public string UserEmail { get; private set; } = string.Empty;

    /// <summary>
    /// Selected grants encoded as "{ResourceType}|{ResourceKey}|{AccessType}".
    /// </summary>
    [BindProperty]
    public List<string> SelectedGrants { get; set; } = [];

    [BindProperty]
    public ResourceType NewResourceType { get; set; } = ResourceType.Application;

    [BindProperty]
    public string NewResourceKey { get; set; } = string.Empty;

    [BindProperty]
    public AccessType NewAccessType { get; set; } = AccessType.Read;

    public IReadOnlyList<ResourceType> ResourceTypes { get; } = Enum.GetValues<ResourceType>().ToArray();

    public IReadOnlyList<AccessType> AccessTypes { get; } = Enum.GetValues<AccessType>().ToArray();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(cancellationToken);
        return loaded ? Page() : RedirectToPage("/Admin/UserManager");
    }

    public async Task<IActionResult> OnPostAddAsync(CancellationToken cancellationToken)
    {
        if (!await LoadUserMetaAsync(cancellationToken))
            return RedirectToPage("/Admin/UserManager");

        SelectedGrants = NormalizeGrants(SelectedGrants);

        var resourceKey = NewResourceKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            ModelState.AddModelError(nameof(NewResourceKey), "Enter a resource key.");
            return Page();
        }

        var validationError = RoleManagerPermissionsModel.ValidateGrant(NewResourceType, resourceKey, NewAccessType);
        if (validationError is not null)
        {
            ModelState.AddModelError(nameof(NewResourceKey), validationError);
            return Page();
        }

        var key = RoleManagerPermissionsModel.EncodeGrantKey(NewResourceType, resourceKey, NewAccessType);
        if (SelectedGrants.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                string.Empty,
                $"{NewResourceType} / {resourceKey} / {NewAccessType} is already in the list.");
        }
        else
        {
            SelectedGrants.Add(key);
            SelectedGrants = NormalizeGrants(SelectedGrants);
            NewResourceKey = string.Empty;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRemoveAsync(string grantKey, CancellationToken cancellationToken)
    {
        if (!await LoadUserMetaAsync(cancellationToken))
            return RedirectToPage("/Admin/UserManager");

        SelectedGrants = NormalizeGrants(SelectedGrants);
        SelectedGrants.RemoveAll(g => string.Equals(g, grantKey, StringComparison.OrdinalIgnoreCase));
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!await LoadUserMetaAsync(cancellationToken))
            return RedirectToPage("/Admin/UserManager");

        SelectedGrants = NormalizeGrants(SelectedGrants);

        foreach (var grant in SelectedGrants.Select(ParseGrantKey).Where(g => g is not null))
        {
            var error = RoleManagerPermissionsModel.ValidateGrant(
                grant!.Value.ResourceType,
                grant.Value.ResourceKey,
                grant.Value.AccessType);
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

            await usersClient.SetUserPermissionsAsync(
                UserId,
                new SetUserPermissionsRequest { Permissions = grants },
                cancellationToken);

            TempData["UserManagerSuccess"] = $"Permissions updated for '{UserName}'.";
            return RedirectToPage("/Admin/UserManager");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set permissions for user {UserId}", UserId);
            ModelState.AddModelError(string.Empty, UserManagerModel.GetErrorMessage(ex, "Could not save permissions."));
            return Page();
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        if (!await LoadUserMetaAsync(cancellationToken))
            return false;

        try
        {
            var existing = await usersClient.GetUserPermissionsAsync(UserId, cancellationToken);
            SelectedGrants = NormalizeGrants(
                existing?
                    .Select(p => RoleManagerPermissionsModel.EncodeGrantKey(p.ResourceType, p.ResourceKey, p.AccessType))
                    .ToList() ?? []);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load permissions for user {UserId}", UserId);
            TempData["UserManagerError"] = UserManagerModel.GetErrorMessage(ex, "Could not load user permissions.");
            return false;
        }
    }

    private async Task<bool> LoadUserMetaAsync(CancellationToken cancellationToken)
    {
        try
        {
            var users = await usersClient.GetTenantUsersAsync(cancellationToken);
            var user = users?.FirstOrDefault(u => u.UserId == UserId);
            if (user is null)
            {
                TempData["UserManagerError"] = "User not found.";
                return false;
            }

            UserName = user.Name;
            UserEmail = user.Email;
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load user {UserId}", UserId);
            TempData["UserManagerError"] = UserManagerModel.GetErrorMessage(ex, "Could not load user.");
            return false;
        }
    }

    private static List<string> NormalizeGrants(IEnumerable<string>? grants) =>
        (grants ?? [])
            .Select(ParseGrantKey)
            .Where(g => g is not null)
            .Select(g => RoleManagerPermissionsModel.EncodeGrantKey(
                g!.Value.ResourceType,
                g.Value.ResourceKey,
                g.Value.AccessType))
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
