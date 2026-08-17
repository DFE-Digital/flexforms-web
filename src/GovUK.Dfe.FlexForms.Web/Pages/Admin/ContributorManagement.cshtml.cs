using System.ComponentModel.DataAnnotations;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Lookup application contributors by reference number for tenant admins.
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

    [BindProperty]
    [Required(ErrorMessage = "Enter an application reference number")]
    [StringLength(100)]
    public string ReferenceNumber { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ReferenceNumber = ReferenceNumber?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
            return Page();

        var state = new ContributorManagementWorkState { ReferenceNumber = ReferenceNumber };
        await contributorManagementAdmin.LookupAsync(state, cancellationToken);
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
        if (state.HasError)
        {
            HasError = true;
            ErrorMessage = state.ErrorMessage;
        }
    }
}
