using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Pages.Error;

/// <summary>
/// Page model for general API errors (e.g., 429 Rate Limit)
/// </summary>
[ExcludeFromCodeCoverage]
public class GeneralModel : PageModel
{
    private readonly IConfiguration _configuration;

    public string SupportEmail { get; private set; } = string.Empty;
    public string EmailSubject { get; private set; } = string.Empty;
    public string EmailBody { get; private set; } = string.Empty;
    public string? ErrorId { get; private set; }
    public string? ErrorMessage { get; private set; }

    public GeneralModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnGet()
    {
        // Get API error ID and message from TempData (set by ExternalApiExceptionFilter)
        ErrorId = TempData["ApiErrorId"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;
        
        // Get support email from configuration
        SupportEmail = _configuration["SupportEmail"] ?? "RegionalServices.RG@education.gov.uk";
        
        // Prepare email subject and body
        var serviceName = _configuration["Layout:ServiceName"] ?? "External Applications";
        EmailSubject = $"Error - {serviceName}";
        EmailBody = $"I encountered an error while using the {serviceName} service.\n\n" +
                   $"Error Reference: {ErrorId ?? "N/A"}\n" +
                   $"Error Message: {ErrorMessage ?? "N/A"}\n\n" +
                   $"Please provide details about what you were trying to do when the error occurred:";
    }
}
