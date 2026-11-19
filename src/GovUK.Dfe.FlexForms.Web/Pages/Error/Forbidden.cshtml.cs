using GovUK.Dfe.FlexForms.Web.Constants;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Pages.Error;

/// <summary>
/// Page model for 403 Forbidden error page
/// </summary>
[ExcludeFromCodeCoverage]
public class ForbiddenModel : PageModel
{
    private readonly IConfiguration _configuration;

    public string SupportEmail { get; private set; } = string.Empty;
    public string EmailSubject { get; private set; } = string.Empty;
    public string EmailBody { get; private set; } = string.Empty;
    public string? ErrorId { get; private set; }
    public string? DenialReason { get; private set; }

    /// <summary>
    /// True when the user signed in to a tenant they are not a member of. They have been signed out
    /// of this service, so the dashboard link would only sign them in again and return here.
    /// </summary>
    public bool IsTenantMembershipDenial { get; private set; }

    public ForbiddenModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnGet()
    {
        ErrorId = TempData["ApiErrorId"] as string;
        DenialReason = TempData["AccessDeniedReason"] as string
            ?? ApplicationAccessMessages.NoAccess;
        IsTenantMembershipDenial = string.Equals(ErrorId, TenantMembershipDenial.ErrorId, StringComparison.Ordinal);

        SupportEmail = _configuration["SupportEmail"] ?? "RegionalServices.RG@education.gov.uk";

        var serviceName = _configuration["Layout:ServiceName"] ?? "External Applications";
        EmailSubject = $"Access Denied - {serviceName}";
        EmailBody = $"I was denied access to a page in the {serviceName} service.\n\n" +
                   $"Error Reference: {ErrorId ?? "N/A"}\n\n" +
                   $"Please provide details about what you were trying to access:";
    }
}
