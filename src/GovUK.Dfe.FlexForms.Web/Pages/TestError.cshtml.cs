using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Pages;

/// <summary>
/// Test page for triggering different error scenarios to test error handling
/// Only available in Development and Test environments
/// </summary>
[ExcludeFromCodeCoverage]
public class TestErrorModel : PageModel
{
    private readonly ILogger<TestErrorModel> _logger;
    private readonly IHostEnvironment _environment;

    public TestErrorModel(ILogger<TestErrorModel> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public IActionResult OnGet([FromQuery] string? trigger = null)
    {
        // Only allow in Development, Test, or Staging environments
        if (!(_environment.IsDevelopment() || _environment.IsEnvironment("Test") || _environment.IsStaging()))
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(trigger))
        {
            return Page();
        }

        // Trigger different error types based on query parameter
        switch (trigger.ToLower())
        {
            case "500":
                _logger.LogError("Test 500 error triggered by user");
                throw new InvalidOperationException("This is a test 500 Internal Server Error. Error ID: " + Guid.NewGuid().ToString("N")[..8]);
            
            case "null":
                _logger.LogError("Test NullReferenceException triggered by user");
                string? nullString = null;
                return Content(nullString!.ToString()); // This will throw NullReferenceException
            
            case "argument":
                _logger.LogError("Test ArgumentException triggered by user");
                throw new ArgumentException("This is a test ArgumentException with invalid parameter", "testParameter");
            
            case "unauthorized":
                _logger.LogError("Test 401 Unauthorized triggered by user");
                return Unauthorized();
            
            case "forbidden":
                _logger.LogError("Test 403 Forbidden triggered by user");
                return Forbid();
            
            default:
                return Page();
        }
    }
}

