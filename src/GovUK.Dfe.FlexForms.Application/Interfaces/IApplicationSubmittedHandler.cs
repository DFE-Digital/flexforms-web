using GovUK.Dfe.FlexForms.Application.Models;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Performs one action when an application has been submitted (e.g. publish event, call webhook).
/// Handlers are resolved by key from ApplicationSubmission:Handlers and run by the orchestrator.
/// </summary>
public interface IApplicationSubmittedHandler
{
    /// <summary>
    /// Handles the submission (e.g. map and publish events, call webhook). Implementations should not throw to avoid failing the overall submission.
    /// </summary>
    /// <param name="context">Submitted application, form data, and template</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleAsync(ApplicationSubmittedContext context, CancellationToken cancellationToken = default);
}
