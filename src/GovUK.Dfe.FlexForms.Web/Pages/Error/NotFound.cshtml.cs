using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Pages.Error;

/// <summary>
/// Page model for 404 Not Found error page
/// </summary>
[ExcludeFromCodeCoverage]
public class NotFoundModel : PageModel
{
    private readonly IConfiguration _configuration;

    public string SupportEmail { get; private set; } = string.Empty;
    public string EmailSubject { get; private set; } = string.Empty;
    public string EmailBody { get; private set; } = string.Empty;

    public NotFoundModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnGet()
    {
        // Get support email from configuration
        SupportEmail = _configuration["SupportEmail"] ?? "RegionalServices.RG@education.gov.uk";
        
        // Prepare email subject and body
        var requestedPath = HttpContext.Request.Path.Value ?? "unknown";
        EmailSubject = "Page not found - External Applications";
        EmailBody = $"I tried to access the following page but received a 404 error:\n\nURL: {requestedPath}\n\nPlease provide details about what you were trying to do:";
    }
}

