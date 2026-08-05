using System.Reflection;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Models;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Domain.Models.Messaging;
using GovUK.Dfe.CoreLibs.Messaging.MassTransit.Interfaces;
using GovUK.Dfe.CoreLibs.Messaging.MassTransit.Models;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Handles application submission by mapping form data to configured event types and publishing each to the service bus.
/// Supports typed CoreLibs events and tenant schema events (Phase 3).
/// </summary>
public class PublishEventApplicationSubmittedHandler(
    IEventDataMapperFactory mapperFactory,
    IEventPublisher publishEndpoint,
    ISendEndpointProvider sendEndpointProvider,
    IEventTypeRegistry eventTypeRegistry,
    ISchemaEventDefinitionProvider schemaEventDefinitionProvider,
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
                var kind = string.IsNullOrWhiteSpace(entry.EventKind)
                    ? EventPublishKind.Typed
                    : entry.EventKind.Trim();

                if (string.Equals(kind, EventPublishKind.Schema, StringComparison.OrdinalIgnoreCase))
                {
                    await PublishSchemaEventAsync(
                        mapper,
                        entry,
                        context,
                        applicationId,
                        applicationReference,
                        cancellationToken);
                }
                else
                {
                    await PublishTypedEventAsync(
                        mapper,
                        entry,
                        context,
                        applicationId,
                        applicationReference,
                        cancellationToken);
                }
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

    private async Task PublishTypedEventAsync(
        IEventDataMapper mapper,
        EventEntryOptions entry,
        ApplicationSubmittedContext context,
        Guid applicationId,
        string applicationReference,
        CancellationToken cancellationToken)
    {
        var eventType = eventTypeRegistry.GetEventType(entry.EventType);
        if (eventType == null)
        {
            logger.LogWarning("Event type '{EventType}' is not registered. Skipping.", entry.EventType);
            return;
        }

        var eventData = await MapToEventAsync(
            mapper,
            eventType,
            context.FormData,
            context.Template,
            entry.MappingId,
            applicationId,
            applicationReference,
            cancellationToken);
        if (eventData == null)
        {
            logger.LogWarning("Mapping returned null for event type '{EventType}'. Skipping.", entry.EventType);
            return;
        }

        var messageProperties = AzureServiceBusMessagePropertiesBuilder
            .Create()
            .AddCustomProperty("serviceName", "extweb")
            .AddCustomProperty("eventKind", EventPublishKind.Typed)
            .Build();

        await PublishAsync(publishEndpoint, eventType, eventData, messageProperties, cancellationToken);

        logger.LogInformation(
            "Successfully published typed {EventType} for application {ApplicationId} with reference {ApplicationReference}",
            entry.EventType,
            applicationId,
            applicationReference);
    }

    private async Task PublishSchemaEventAsync(
        IEventDataMapper mapper,
        EventEntryOptions entry,
        ApplicationSubmittedContext context,
        Guid applicationId,
        string applicationReference,
        CancellationToken cancellationToken)
    {
        var definition = schemaEventDefinitionProvider.GetDefinition(entry.EventType);
        if (definition is null || string.IsNullOrWhiteSpace(definition.TopicName))
        {
            logger.LogWarning(
                "Schema event '{EventType}' is not defined in SchemaEvents (or topicName is missing). Skipping.",
                entry.EventType);
            return;
        }

        // Map using the same field-mapping DSL; materialise as a dictionary payload (not a CLR contract).
        var payload = await mapper.MapToDictionaryAsync(
            context.FormData,
            context.Template,
            entry.EventType,
            entry.MappingId,
            applicationId,
            applicationReference,
            cancellationToken);

        var envelope = new SchemaEventEnvelope
        {
            MessageType = entry.EventType,
            Version = string.IsNullOrWhiteSpace(definition.Version) ? "1.0" : definition.Version,
            TopicName = definition.TopicName,
            Payload = payload,
            Metadata = new Dictionary<string, object?>
            {
                ["applicationId"] = applicationId.ToString(),
                ["applicationReference"] = applicationReference,
                ["templateId"] = context.Template.TemplateId
            }
        };

        var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri($"topic:{definition.TopicName}"));

        await endpoint.Send(envelope, sendContext =>
        {
            sendContext.Headers.Set("MessageType", entry.EventType);
            sendContext.Headers.Set("EventKind", EventPublishKind.Schema);
            sendContext.Headers.Set("serviceName", "extweb");
            if (!string.IsNullOrWhiteSpace(definition.Version))
                sendContext.Headers.Set("SchemaVersion", definition.Version);
        }, cancellationToken);

        logger.LogInformation(
            "Successfully published schema event {EventType} to topic {Topic} for application {ApplicationId}",
            entry.EventType,
            definition.TopicName,
            applicationId);
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
