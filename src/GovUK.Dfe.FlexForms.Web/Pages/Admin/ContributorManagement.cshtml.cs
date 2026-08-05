using System.ComponentModel.DataAnnotations;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Lookup application contributors by reference number for tenant admins.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageUsersPolicy)]
public sealed class ContributorManagementModel(
    IApplicationsClient applicationsClient,
    ILogger<ContributorManagementModel> logger) : PageModel
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

        LookupPerformed = true;
        ApplicationReference = ReferenceNumber;

        try
        {
            var application = await applicationsClient.GetApplicationByReferenceAsync(
                ReferenceNumber,
                cancellationToken);

            ApplicationId = application.ApplicationId;
            ApplicationReference = string.IsNullOrWhiteSpace(application.ApplicationReference)
                ? ReferenceNumber
                : application.ApplicationReference;
            TemplateName = application.TemplateName;

            var contributors = await applicationsClient.GetContributorsAsync(
                application.ApplicationId,
                includePermissionDetails: false,
                cancellationToken);

            Contributors = contributors?
                .OrderBy(c => c.Name)
                .ThenBy(c => c.Email)
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to look up contributors for {ReferenceNumber}", ReferenceNumber);
            HasError = true;
            ErrorMessage = GetErrorMessage(ex, "Could not find that application or load its contributors.");
            Contributors = [];
        }

        return Page();
    }

    internal static string GetErrorMessage(Exception ex, string fallback)
    {
        if (ex is ExternalApplicationsException<ExceptionResponse> apiEx
            && !string.IsNullOrWhiteSpace(apiEx.Result?.Message))
        {
            return apiEx.Result.Message;
        }

        if (ex is ExternalApplicationsException clientEx && clientEx.StatusCode > 0)
            return $"{fallback} (HTTP {clientEx.StatusCode})";

        return fallback;
    }
}
