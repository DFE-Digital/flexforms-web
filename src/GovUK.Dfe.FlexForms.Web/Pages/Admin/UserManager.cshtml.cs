using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Lists users with form access in the current tenant.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageUsersPolicy)]
public sealed class UserManagerModel(IUserManagerAdmin userManagerAdmin) : PageModel
{
    public IReadOnlyList<TenantUserDto> Users { get; private set; } = [];

    public IReadOnlyList<TenantAccessAuditEntryDto> AccessAuditEntries { get; private set; } = [];

    public bool HasError { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool ShowSuccess { get; private set; }

    public string? SuccessMessage { get; private set; }

    public bool AuditLogLoadFailed { get; private set; }

    public string? AuditLogLoadErrorMessage { get; private set; }

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; }

    public int PageSize { get; private set; } = UserManagerWorkState.PageSize;

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ApplyTempData();
        var state = new UserManagerWorkState { CurrentPage = CurrentPage };
        await userManagerAdmin.LoadAsync(state, cancellationToken);
        ApplyWorkState(state);
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid userId, CancellationToken cancellationToken)
    {
        return MapOutcome(await userManagerAdmin.RemoveAsync(new UserManagerWorkState(), userId, cancellationToken));
    }

    public string BuildPaginationHref(int page) => $"?currentPage={page}";

    private void ApplyWorkState(UserManagerWorkState state)
    {
        Users = state.Users;
        AccessAuditEntries = state.AccessAuditEntries;
        AuditLogLoadFailed = state.AuditLogLoadFailed;
        AuditLogLoadErrorMessage = state.AuditLogLoadErrorMessage;
        TotalCount = state.TotalCount;
        TotalPages = state.TotalPages;
        PageSize = UserManagerWorkState.PageSize;
        CurrentPage = state.CurrentPage == 0 ? 1 : state.CurrentPage;
        if (state.HasError)
        {
            HasError = true;
            ErrorMessage = state.ErrorMessage;
        }
    }

    private IActionResult MapOutcome(AdminPageOutcome outcome)
    {
        if (outcome.SuccessMessage != null)
            TempData["UserManagerSuccess"] = outcome.SuccessMessage;

        if (outcome.ErrorMessage != null)
            TempData["UserManagerError"] = outcome.ErrorMessage;

        return RedirectToPage(new { currentPage = CurrentPage });
    }

    private void ApplyTempData()
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
    }
}
