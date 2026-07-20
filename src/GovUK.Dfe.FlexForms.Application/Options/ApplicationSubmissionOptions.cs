namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// Configuration for application submission behaviour: which mapper to use and which handlers run on submit.
/// Bound from ApplicationSubmission section in appsettings (per application).
/// </summary>
public class ApplicationSubmissionOptions
{
    /// <summary>
    /// Key used to resolve IEventDataMapper from keyed services (e.g. "Default", "Lsrp").
    /// </summary>
    public string MapperKey { get; set; } = "Default";

    /// <summary>
    /// Ordered list of handler keys to run when an application is submitted (e.g. ["PublishEvent", "Webhook"]).
    /// Each key is resolved and executed once; empty list means no handlers run.
    /// </summary>
    public List<string> Handlers { get; set; } = [];

    /// <summary>
    /// Settings for the PublishEvent handler (event publishing to service bus).
    /// </summary>
    public PublishEventOptions PublishEvent { get; set; } = new();
}
