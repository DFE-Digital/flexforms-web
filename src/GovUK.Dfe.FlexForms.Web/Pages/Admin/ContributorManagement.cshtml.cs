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
[Authorize(Roles = AdminAccessHelper.AuthorizeRoles)]
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

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; }

    public int PageSize { get; private set; } = ContributorManagementWorkState.EmailLookupPageSize;

    [BindProperty]
    public string? ReferenceNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Email))
            return;

        Email = Email.Trim();
        await LookupByEmailAsync(cancellationToken);
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

    public IActionResult OnPostLookupByEmail()
    {
        ModelState.Clear();
        Email = Email?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Email))
        {
            ModelState.AddModelError(nameof(Email), "Enter an email address");
            return Page();
        }

        return RedirectToPage(new { email = Email, currentPage = 1 });
    }

    public string BuildEmailLookupHref(int page) =>
        $"?email={Uri.EscapeDataString(Email ?? string.Empty)}&currentPage={page}";

    private async Task LookupByEmailAsync(CancellationToken cancellationToken)
    {
        var state = new ContributorManagementWorkState
        {
            Email = Email ?? string.Empty,
            CurrentPage = CurrentPage
        };
        await contributorManagementAdmin.LookupByEmailAsync(state, cancellationToken);
        ApplyWorkState(state);
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
        TotalCount = state.TotalCount;
        TotalPages = state.TotalPages;
        PageSize = state.PageSize;
        CurrentPage = state.CurrentPage == 0 ? 1 : state.CurrentPage;
        Email = string.IsNullOrWhiteSpace(state.Email) ? Email : state.Email;
        ReferenceNumber = string.IsNullOrWhiteSpace(state.ReferenceNumber) ? ReferenceNumber : state.ReferenceNumber;
        if (state.HasError)
        {
            HasError = true;
            ErrorMessage = state.ErrorMessage;
        }
    }
}
