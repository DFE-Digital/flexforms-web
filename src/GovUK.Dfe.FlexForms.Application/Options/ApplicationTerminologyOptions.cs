namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// Configuration for customising the display terminology used for the domain concept "Application"
/// throughout the service. Allows each service (e.g. Transfers, LSRP) to use different terms.
/// Bound from the ApplicationTerminology section in appsettings.
/// </summary>
public class ApplicationTerminologyOptions
{
    /// <summary>
    /// The singular form of the term in lowercase (e.g. "application", "reform plan").
    /// </summary>
    public string Singular { get; set; } = "application";

    /// <summary>
    /// The plural form of the term in lowercase (e.g. "applications", "reform plans").
    /// </summary>
    public string Plural { get; set; } = "applications";
}
