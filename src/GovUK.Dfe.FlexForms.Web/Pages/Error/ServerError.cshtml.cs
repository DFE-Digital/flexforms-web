using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Pages.Error;

/// <summary>
/// Page model for 500 Internal Server Error page
/// </summary>
[ExcludeFromCodeCoverage]
public class ServerErrorModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServerErrorModel> _logger;

    public string SupportEmail { get; private set; } = string.Empty;
    public string EmailSubject { get; private set; } = string.Empty;
    public string EmailBody { get; private set; } = string.Empty;
    public string? ErrorId { get; private set; }

    public ServerErrorModel(IConfiguration configuration, ILogger<ServerErrorModel> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void OnGet([FromQuery] string? errorId = null)
    {
        // Check for API error ID from TempData first (set by ExternalApiExceptionFilter)
        var apiErrorId = TempData["ApiErrorId"] as string;
        
        // Priority: API error ID > query parameter > generate new
        ErrorId = apiErrorId ?? errorId ?? Guid.NewGuid().ToString("N")[..8];
        
        // Get support email from configuration
        SupportEmail = _configuration["SupportEmail"] ?? "RegionalServices.RG@education.gov.uk";
        
        // Prepare email subject and body
        var requestedPath = HttpContext.Request.Path.Value ?? "unknown";
        var serviceName = _configuration["Layout:ServiceName"] ?? "External Applications";
        EmailSubject = $"Server Error - {serviceName}";
        EmailBody = $"I encountered a server error while using the {serviceName} service.\n\n" +
                   $"Error Reference: {ErrorId}\n\n" +
                   $"Please provide details about what you were trying to do when the error occurred:";
        
        _logger.LogError("Server error page displayed. ErrorId: {ErrorId}, Path: {Path}, Source: {Source}", 
            ErrorId, requestedPath, apiErrorId != null ? "API" : errorId != null ? "Query" : "Generated");
    }
}

