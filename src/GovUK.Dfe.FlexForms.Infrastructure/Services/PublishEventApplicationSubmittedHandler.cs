using System.Reflection;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Models;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.CoreLibs.Messaging.MassTransit.Interfaces;
using GovUK.Dfe.CoreLibs.Messaging.MassTransit.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Handles application submission by mapping form data to configured event types and publishing each to the service bus.
/// </summary>
public class PublishEventApplicationSubmittedHandler(
    IEventDataMapperFactory mapperFactory,
    IEventPublisher publishEndpoint,
    IEventTypeRegistry eventTypeRegistry,
    IOptions<ApplicationSubmissionOptions> options,
    ILogger<PublishEventApplicationSubmittedHandler> logger) : IApplicationSubmittedHandler
{
    private static readonly MethodInfo MapToEventAsyncMethod = typeof(IEventDataMapper).GetMethod(nameof(IEventDataMapper.MapToEventAsync))!;

    /// <inheritdoc />
    public async Task HandleAsync(ApplicationSubmittedContext context, CancellationToken cancellationToken = default)
    {
        var publishOptions = options.Value.PublishEvent ?? new PublishEventOptions();
        if (!publishOptions.Enabled || publishOptions.Events == null || publishOptions.Events.Count == 0)
        {
            logger.LogDebug("PublishEvent handler is disabled or has no events configured. Skipping.");
            return;
        }

        var mapper = mapperFactory.GetMapper(cancellationToken);
        var application = context.Application;
        var applicationId = application.ApplicationId;
        var applicationReference = application.ApplicationReference ?? string.Empty;

        foreach (var entry in publishOptions.Events)
        {
            if (string.IsNullOrEmpty(entry.EventType) || string.IsNullOrEmpty(entry.MappingId))
            {
                logger.LogWarning("Skipping event entry with missing EventType or MappingId.");
                continue;
            }

            try
            {
                var eventType = eventTypeRegistry.GetEventType(entry.EventType);
                if (eventType == null)
                {
                    logger.LogWarning("Event type '{EventType}' is not registered. Skipping.", entry.EventType);
                    continue;
                }

                var eventData = await MapToEventAsync(mapper, eventType, context.FormData, context.Template, entry.MappingId, applicationId, applicationReference, cancellationToken);
                if (eventData == null)
                {
                    logger.LogWarning("Mapping returned null for event type '{EventType}'. Skipping.", entry.EventType);
                    continue;
                }

                var messageProperties = AzureServiceBusMessagePropertiesBuilder
                    .Create()
                    .AddCustomProperty("serviceName", "extweb")
                    .Build();

                await PublishAsync(publishEndpoint, eventType, eventData, messageProperties, cancellationToken);

                logger.LogInformation(
                    "Successfully published {EventType} for application {ApplicationId} with reference {ApplicationReference}",
                    entry.EventType,
                    applicationId,
                    applicationReference);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to publish {EventType} for application {ApplicationId}. Application was successfully submitted; continuing with next event.",
                    entry.EventType,
                    applicationId);
            }
        }
    }

    /// <summary>
    /// Calls IEventPublisher.PublishAsync with the concrete event type so MassTransit routes to the correct topic
    /// (publishing as object would cause "Messages types must not be in the System namespace: System.Object").
    /// </summary>
    private static async Task PublishAsync(
        IEventPublisher publishEndpoint,
        Type eventType,
        object eventData,
        object messageProperties,
        CancellationToken cancellationToken)
    {
        var publishMethod = typeof(IEventPublisher)
            .GetMethods()
            .First(m => m.Name == nameof(IEventPublisher.PublishAsync) && m.IsGenericMethodDefinition && m.GetParameters().Length == 3);
        var genericPublish = publishMethod.MakeGenericMethod(eventType);
        var task = genericPublish.Invoke(publishEndpoint, [eventData, messageProperties, cancellationToken]);
        if (task is Task t)
            await t.ConfigureAwait(false);
    }

    private static async Task<object?> MapToEventAsync(
        IEventDataMapper mapper,
        Type eventType,
        Dictionary<string, object> formData,
        FormTemplate template,
        string mappingId,
        Guid applicationId,
        string applicationReference,
        CancellationToken cancellationToken)
    {
        var genericMethod = MapToEventAsyncMethod.MakeGenericMethod(eventType);
        var task = genericMethod.Invoke(mapper, [formData, template, mappingId, applicationId, applicationReference, cancellationToken]);
        if (task is not Task awaitable)
            return null;
        await awaitable.ConfigureAwait(false);
        return awaitable.GetType().GetProperty("Result")!.GetValue(awaitable);
    }
}
