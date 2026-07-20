using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Applications;

/// <summary>
/// Page model for inviting contributors to an application
/// </summary>
[ExcludeFromCodeCoverage]
[Authorize]
public class ContributorsInviteModel(
    IContributorService contributorService,
    IApplicationStateService applicationStateService,
    IContributorPatternService contributorPatternService,
    IRequestAppConfiguration requestConfiguration,
    //IApiErrorParser apiErrorParser,
    //IModelStateErrorHandler errorHandler,
    ILogger<ContributorsInviteModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "referenceNumber")]
    public string ReferenceNumber { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Enter the email address of the person you want to invite as a contributor")]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    [Display(Name = "Email address")]
    public string EmailAddress { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Enter the name of the person you want to invite as a contributor")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    public Guid? ApplicationId { get; private set; }
    public bool HasError { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Handles GET request to display the invite form
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {

        // Ensure we have a valid application ID
        var (applicationId, application) = await applicationStateService.EnsureApplicationIdAsync(ReferenceNumber, HttpContext.Session);

        var redirect = await RedirectIfContributorPatternDisabledAsync(application);
        if (redirect != null)
        {
            return redirect;
        }

        ApplicationId = applicationId;
        return Page();
    }

    /// <summary>
    /// Handles POST request to send contributor invitation
    /// </summary>
    public async Task<IActionResult> OnPostSendInviteAsync()
    {
        var (applicationId, application) = await applicationStateService.EnsureApplicationIdAsync(ReferenceNumber, HttpContext.Session);

        var redirect = await RedirectIfContributorPatternDisabledAsync(application);
        if (redirect != null)
        {
            return redirect;
        }

        ApplicationId = applicationId;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Create invitation request
        var inviteRequest = new AddContributorRequest
        {
            Email = EmailAddress,
            Name = Name
        };

        // Send the invitation
        await contributorService.InviteContributorAsync(applicationId.Value, inviteRequest);

        logger.LogInformation("Successfully invited contributor {Name} ({Email}) to application {ApplicationId}",
            Name, EmailAddress, applicationId.Value);

        // Redirect back to contributors page
        return RedirectToPage("/Applications/Contributors", new { referenceNumber = ReferenceNumber });

    }

    /// <summary>
    /// Handles request to cancel and return to contributors page
    /// </summary>
    public async Task<IActionResult> OnPostCancel()
    {
        logger.LogInformation("User cancelled contributor invitation for application reference {ReferenceNumber}", ReferenceNumber);

        var (_, application) = await applicationStateService.EnsureApplicationIdAsync(ReferenceNumber, HttpContext.Session);
        var redirect = await RedirectIfContributorPatternDisabledAsync(application);
        if (redirect != null)
        {
            return redirect;
        }

        return RedirectToPage("/Applications/Contributors", new { referenceNumber = ReferenceNumber });
    }

    private async Task<IActionResult?> RedirectIfContributorPatternDisabledAsync(ApplicationDto? application = null)
    {
        var templateId = HttpContext.Session.GetString("TemplateId") ?? requestConfiguration["Template:Id"] ?? string.Empty;
        if (await contributorPatternService.IsEnabledAsync(templateId, application))
        {
            return null;
        }

        logger.LogInformation(
            "Contributor pattern disabled for template {TemplateId}; redirecting away from invite page for application {ReferenceNumber}",
            templateId,
            ReferenceNumber);
        return RedirectToPage("/FormEngine/RenderForm", new { referenceNumber = ReferenceNumber });
    }
}
