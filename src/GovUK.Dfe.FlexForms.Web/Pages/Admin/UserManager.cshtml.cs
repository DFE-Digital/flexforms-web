using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Lists users with form access in the current tenant.
/// </summary>
[Authorize(Roles = AdminAccessHelper.AuthorizeRoles)]
public sealed class UserManagerModel(
    IUsersClient usersClient,
    ILogger<UserManagerModel> logger) : PageModel
{
    public IReadOnlyList<TenantUserDto> Users { get; private set; } = [];

    public bool HasError { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool ShowSuccess { get; private set; }

    public string? SuccessMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (TempData["UserManagerSuccess"] is string success)
        {
            ShowSuccess = true;
            SuccessMessage = success;
        }

        if (TempData["UserManagerError"] is string error)
        {
            HasError = true;
            ErrorMessage = error;
        }

        await LoadUsersAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await usersClient.RemoveUserFromTenantAsync(userId, cancellationToken);
            TempData["UserManagerSuccess"] = "User removed from this tenant.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove user {UserId} from tenant", userId);
            TempData["UserManagerError"] = GetErrorMessage(ex, "Could not remove the user from this tenant.");
        }

        return RedirectToPage();
    }

    private async Task LoadUsersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var users = await usersClient.GetTenantUsersAsync(cancellationToken);
            Users = users?.OrderBy(u => u.Name).ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load tenant users");
            HasError = true;
            ErrorMessage = GetErrorMessage(ex, "Could not load users for this tenant.");
            Users = [];
        }
    }

    internal static string GetErrorMessage(Exception ex, string fallback)
    {
        if (ex is ExternalApplicationsException<ExceptionResponse> apiEx
            && !string.IsNullOrWhiteSpace(apiEx.Result?.Message))
        {
            return apiEx.Result.Message;
        }

        return fallback;
    }
}
