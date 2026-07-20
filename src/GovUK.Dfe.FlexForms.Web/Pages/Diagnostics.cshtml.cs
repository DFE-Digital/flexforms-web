using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages;

/// <summary>
/// Diagnostics endpoint that returns basic runtime information as JSON.
/// </summary>
[AllowAnonymous]
public class DiagnosticsModel(IHostEnvironment hostEnvironment) : PageModel
{
    /// <summary>
    /// Returns service name, environment name, and the assembly informational version.
    /// </summary>
    public IActionResult OnGet()
    {
        var assembly = typeof(Program).Assembly;
        var serviceName = assembly.GetName().Name ?? "Unknown";
        var environmentName = hostEnvironment.EnvironmentName;

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var version = string.IsNullOrWhiteSpace(informationalVersion)
            ? (assembly.GetName().Version?.ToString() ?? "Unknown")
            : informationalVersion;

        return new JsonResult(new
        {
            service = serviceName,
            environment = environmentName,
            version
        });
    }
}


