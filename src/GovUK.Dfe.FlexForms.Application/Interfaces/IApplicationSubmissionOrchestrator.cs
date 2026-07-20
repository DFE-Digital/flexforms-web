using GovUK.Dfe.FlexForms.Application.Models;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Orchestrates post-submission behaviour: runs the list of configured handlers (e.g. PublishEvent, Webhook) when an application is submitted.
/// </summary>
public interface IApplicationSubmissionOrchestrator
{
    /// <summary>
    /// Executes the configured submission handlers in order (from ApplicationSubmission:Handlers).
    /// Each handler is run once; failures in a handler are logged and do not stop subsequent handlers.
    /// </summary>
    /// <param name="application">The submitted application</param>
    /// <param name="formData">Accumulated form data at submission time</param>
    /// <param name="template">The form template</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExecuteOnSubmittedAsync(
        ApplicationDto application,
        Dictionary<string, object> formData,
        FormTemplate template,
        CancellationToken cancellationToken = default);
}
