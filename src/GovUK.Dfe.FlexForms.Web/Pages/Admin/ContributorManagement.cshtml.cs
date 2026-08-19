using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Lookup application contributors by reference number, or look up who a user invited by email.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageUsersPolicy)]
public sealed class ContributorManagementModel(IContributorManagementAdmin contributorManagementAdmin) : PageModel
{
    public bool HasError { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool LookupPerformed { get; private set; }

    public string? ApplicationReference { get; private set; }

    public Guid? ApplicationId { get; private set; }

    public string? TemplateName { get; private set; }

    public IReadOnlyList<UserDto> Contributors { get; private set; } = [];

    public bool EmailLookupPerformed { get; private set; }

    public Guid? LookedUpUserId { get; private set; }

    public string? LookedUpUserName { get; private set; }

    public string? LookedUpUserEmail { get; private set; }

    public IReadOnlyList<CreatedApplicationInviteSummary> CreatedApplications { get; private set; } = [];

    [BindProperty]
    public string? ReferenceNumber { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostLookupByReferenceAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        ReferenceNumber = ReferenceNumber?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(ReferenceNumber))
        {
            ModelState.AddModelError(nameof(ReferenceNumber), "Enter an application reference number");
            return Page();
        }

        var state = new ContributorManagementWorkState { ReferenceNumber = ReferenceNumber };
        await contributorManagementAdmin.LookupAsync(state, cancellationToken);
        ApplyWorkState(state);
        return Page();
    }

    public async Task<IActionResult> OnPostLookupByEmailAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        Email = Email?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Email))
        {
            ModelState.AddModelError(nameof(Email), "Enter an email address");
            return Page();
        }

        var state = new ContributorManagementWorkState { Email = Email };
        await contributorManagementAdmin.LookupByEmailAsync(state, cancellationToken);
        ApplyWorkState(state);
        return Page();
    }

    private void ApplyWorkState(ContributorManagementWorkState state)
    {
        LookupPerformed = state.LookupPerformed;
        ApplicationReference = state.ApplicationReference;
        ApplicationId = state.ApplicationId;
        TemplateName = state.TemplateName;
        Contributors = state.Contributors;
        EmailLookupPerformed = state.EmailLookupPerformed;
        LookedUpUserId = state.LookedUpUserId;
        LookedUpUserName = state.LookedUpUserName;
        LookedUpUserEmail = state.LookedUpUserEmail;
        CreatedApplications = state.CreatedApplications;
        Email = string.IsNullOrWhiteSpace(state.Email) ? Email : state.Email;
        ReferenceNumber = string.IsNullOrWhiteSpace(state.ReferenceNumber) ? ReferenceNumber : state.ReferenceNumber;
        if (state.HasError)
        {
            HasError = true;
            ErrorMessage = state.ErrorMessage;
        }
    }
}
