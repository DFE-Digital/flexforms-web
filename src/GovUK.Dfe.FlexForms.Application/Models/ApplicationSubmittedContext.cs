using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Models;

/// <summary>
/// Context passed to application submitted handlers (form data, template, submitted application).
/// </summary>
public class ApplicationSubmittedContext
{
    /// <summary>
    /// The application that was just submitted (from the API).
    /// </summary>
    public required ApplicationDto Application { get; init; }

    /// <summary>
    /// Accumulated form data at submission time (already unwrapped by the form engine).
    /// </summary>
    public required Dictionary<string, object> FormData { get; init; }

    /// <summary>
    /// The form template for the application.
    /// </summary>
    public required FormTemplate Template { get; init; }
}
