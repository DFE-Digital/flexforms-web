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

    public ForbiddenModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnGet()
    {
        // Get API error ID from TempData (set by ExternalApiExceptionFilter)
        ErrorId = TempData["ApiErrorId"] as string;
        
        // Get support email from configuration
        SupportEmail = _configuration["SupportEmail"] ?? "RegionalServices.RG@education.gov.uk";
        
        // Prepare email subject and body
        var serviceName = _configuration["Layout:ServiceName"] ?? "External Applications";
        EmailSubject = $"Access Denied - {serviceName}";
        EmailBody = $"I was denied access to a page in the {serviceName} service.\n\n" +
                   $"Error Reference: {ErrorId ?? "N/A"}\n\n" +
                   $"Please provide details about what you were trying to access:";
    }
}
