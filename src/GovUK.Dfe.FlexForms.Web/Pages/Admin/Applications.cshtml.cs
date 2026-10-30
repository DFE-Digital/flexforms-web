using System.Diagnostics.CodeAnalysis;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Web.Models.Applications;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Admin browser of all applications for a selected tenant template (Admin / SuperAdmin).
/// </summary>
[ExcludeFromCodeCoverage]
[Authorize(Roles = AdminAccessHelper.AuthorizeRoles)]
public class ApplicationsModel(
    IApplicationsClient applicationsClient,
    IApplicationOwnerClient applicationOwnerClient,
    ITemplateSelectionService templateSelectionService,
    IOptions<DashboardOptions> dashboardOptions,
    ILogger<ApplicationsModel> logger) : PageModel
{
    public IReadOnlyList<TemplateDto> Templates { get; private set; } = [];

    public IReadOnlyList<ApplicationDto> Applications { get; private set; } = [];

    public int PageSize => dashboardOptions.Value.PageSize;

    public int TotalPages { get; private set; }

    public bool HasError { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? SuccessMessage { get; private set; }

    [BindProperty(SupportsGet = true)]
    public Guid? SelectedTemplateId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        SuccessMessage = TempData["OwnerChangeSuccess"] as string;
        ErrorMessage = TempData["OwnerChangeError"] as string;
        HasError = ErrorMessage is not null;
        await LoadTemplatesAsync(cancellationToken);

        if (SelectedTemplateId is null || SelectedTemplateId == Guid.Empty)
        {
            Applications = [];
            return;
        }

        if (Templates.All(t => t.TemplateId != SelectedTemplateId.Value))
        {
            HasError = true;
            ErrorMessage = "The selected template was not found in this tenant.";
            SelectedTemplateId = null;
            Applications = [];
            return;
        }

        await LoadApplicationsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteActionAsync(Guid selectedTemplateId, Guid applicationId, CancellationToken cancellationToken)
    {
        await applicationsClient.DeleteApplicationAsync(applicationId, cancellationToken);
        CurrentPage = 1; // Reset to first page after deletion/un-deletion
        
        return RedirectToPage(new {selectedTemplateId, CurrentPage});
    }

    public async Task<IActionResult> OnPostChangeOwnerAsync(
        Guid selectedTemplateId,
        Guid applicationId,
        string email,
        CancellationToken cancellationToken)
    {
        SelectedTemplateId = selectedTemplateId;
        var message = "The lead applicant could not be changed. Please try again.";

        try
        {
            var result = await applicationOwnerClient.ChangeOwnerAsync(applicationId, email, cancellationToken);
            TempData["OwnerChangeSuccess"] = result.TemplateReadGranted
                ? "Lead applicant updated. They were given read access to this template."
                : "Lead applicant updated.";
            return RedirectToPage(new { selectedTemplateId, CurrentPage });
        }
        catch (ExternalApplicationsException ex) when (ex.StatusCode == 404)
        {
            message = "That user was not found in this tenant.";
        }
        catch (ExternalApplicationsException ex) when (ex.StatusCode == 400)
        {
            if (ex.Response?.Contains("already the lead applicant", StringComparison.OrdinalIgnoreCase) == true)
                message = "This user is already the lead applicant.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to change lead applicant for application {ApplicationId}", applicationId);
        }

        TempData["OwnerChangeError"] = message;
        return RedirectToPage(new { selectedTemplateId, CurrentPage });
    }

    private async Task LoadTemplatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            Templates = await templateSelectionService.GetSelectableTemplatesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load templates for admin applications browser");
            HasError = true;
            ErrorMessage = "Failed to load templates. Please try again.";
            Templates = [];
        }
    }

    private async Task LoadApplicationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await applicationsClient.GetApplicationsByTemplateAsync(
                templateId: SelectedTemplateId!.Value,
                pageNumber: CurrentPage,
                pageSize: PageSize,
                cancellationToken: cancellationToken);

            TotalPages = result.TotalPages;
            CurrentPage = Math.Clamp(CurrentPage, 1, Math.Max(1, TotalPages));

            Applications = result.Items.OrderByDescending(a => a.DateCreated).ToList();            
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to load applications for template {TemplateId}",
                SelectedTemplateId);
            HasError = true;
            ErrorMessage = "Failed to load applications for the selected template. Please try again.";
            Applications = [];
        }
    }
}
