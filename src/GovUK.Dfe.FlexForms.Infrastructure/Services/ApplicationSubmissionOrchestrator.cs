using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Models;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Runs the list of configured submission handlers in order; logs and continues if a handler fails.
/// </summary>
public class ApplicationSubmissionOrchestrator(
    IServiceProvider serviceProvider,
    IOptions<ApplicationSubmissionOptions> options,
    ILogger<ApplicationSubmissionOrchestrator> logger) : IApplicationSubmissionOrchestrator
{
    /// <inheritdoc />
    public async Task ExecuteOnSubmittedAsync(
        ApplicationDto application,
        Dictionary<string, object> formData,
        FormTemplate template,
        CancellationToken cancellationToken = default)
    {
        var handlerKeys = options.Value.Handlers ?? [];
        if (handlerKeys.Count == 0)
        {
            logger.LogDebug("No submission handlers configured for application {ApplicationId}", application.ApplicationId);
            return;
        }

        var context = new ApplicationSubmittedContext
        {
            Application = application,
            FormData = formData,
            Template = template
        };

        foreach (var key in handlerKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var handler = serviceProvider.GetKeyedService<IApplicationSubmittedHandler>(key);
            if (handler == null)
            {
                logger.LogWarning("No IApplicationSubmittedHandler registered for key '{HandlerKey}'. Skipping.", key);
                continue;
            }

            try
            {
                await handler.HandleAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Submission handler '{HandlerKey}' failed for application {ApplicationId}. Continuing with next handler.",
                    key,
                    application.ApplicationId);
            }
        }
    }
}
