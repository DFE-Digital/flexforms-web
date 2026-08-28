using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.Models.EventMapping;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Loads and persists EventMappings, SchemaEvents, and EventTriggers for the current tenant.
/// </summary>
public interface IEventMappingsAdmin
{
    Task LoadAsync(EventMappingsWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> SaveTriggerAsync(EventMappingsWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> DeleteTriggerAsync(EventMappingsWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> SaveMappingAsync(EventMappingsWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> SaveSchemaAsync(EventMappingsWorkState state, CancellationToken cancellationToken = default);
}

public sealed class EventMappingsAdminService(
    ITenantAdminClient tenantAdminClient,
    ITemplatesClient templatesClient,
    IEventTypeRegistry eventTypeRegistry,
    ISchemaEventDefinitionProvider schemaEventDefinitionProvider,
    ILogger<EventMappingsAdminService> logger) : IEventMappingsAdmin
{
    public const string TargetShared = "Shared";
    public const string TargetWeb = "Web";
    public const string CategoryEventMappings = "EventMappings";
    public const string CategorySchemaEvents = "SchemaEvents";
    public const string CategoryEventTriggers = "EventTriggers";
    public const string SystemOnlyEventType = "ScanRequestedEvent";

    public static readonly string[] TriggerNames = ["ApplicationSubmitted", "FileUploaded"];

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions JsonPersistOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task LoadAsync(EventMappingsWorkState state, CancellationToken cancellationToken = default)
    {
        await LoadPageDataAsync(state, cancellationToken);
        await LoadSavedTypedMappingsAsync(state, cancellationToken);
        await LoadSavedTriggersAsync(state, cancellationToken);
        await LoadSelectedMappingAsync(state, cancellationToken);
        await LoadSelectedSchemaDefinitionAsync(state, cancellationToken);
    }

    public async Task<AdminPageOutcome> SaveTriggerAsync(
        EventMappingsWorkState state,
        CancellationToken cancellationToken = default)
    {
        await LoadAsync(state, cancellationToken);

        var trigger = state.TriggerName?.Trim();
        var eventType = state.TriggerEventType?.Trim();
        var mappingId = state.TriggerMappingId?.Trim();
        var eventKind = string.IsNullOrWhiteSpace(state.TriggerEventKind)
            ? EventPublishKind.Typed
            : state.TriggerEventKind.Trim();

        var errors = new List<FormValidationError>();
        if (string.IsNullOrWhiteSpace(trigger)
            || !TriggerNames.Contains(trigger, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(new FormValidationError(nameof(EventMappingsWorkState.TriggerName), EventMappingsMessages.SelectTrigger));
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            errors.Add(new FormValidationError(nameof(EventMappingsWorkState.TriggerEventType), EventMappingsMessages.SelectEventType));
        }
        else if (string.Equals(eventType, SystemOnlyEventType, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new FormValidationError(
                nameof(EventMappingsWorkState.TriggerEventType),
                EventMappingsMessages.SystemOnlyEventType(SystemOnlyEventType)));
        }

        if (string.IsNullOrWhiteSpace(mappingId))
            errors.Add(new FormValidationError(nameof(EventMappingsWorkState.TriggerMappingId), EventMappingsMessages.EnterMappingId));

        if (!string.Equals(eventKind, EventPublishKind.Typed, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(eventKind, EventPublishKind.Schema, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new FormValidationError(
                nameof(EventMappingsWorkState.TriggerEventKind),
                EventMappingsMessages.EventKindMustBeTypedOrSchema));
        }

        if (errors.Count > 0)
            return Stay(state, errors);

        try
        {
            var root = await LoadCategoryRootAsync(state, CategoryEventTriggers, cancellationToken);
            var bindings = root[trigger!] as JsonArray ?? new JsonArray();

            var replaced = false;
            for (var i = 0; i < bindings.Count; i++)
            {
                if (bindings[i] is not JsonObject existing)
                    continue;

                if (!string.Equals(ReadBindingValue(existing, "eventType"), eventType, StringComparison.OrdinalIgnoreCase))
                    continue;

                bindings[i] = BuildBindingNode(eventKind, eventType!, mappingId!);
                replaced = true;
                break;
            }

            if (!replaced)
                bindings.Add(BuildBindingNode(eventKind, eventType!, mappingId!));

            root[trigger!] = bindings;

            await UpsertCategoryAsync(state, CategoryEventTriggers, root, cancellationToken);
            await tenantAdminClient.RefreshTenantConfigurationAsync(cancellationToken);

            return Redirect(
                state,
                EventMappingsMessages.SavedTrigger(eventType!, trigger!),
                refreshLocalCaches: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save EventTriggers for {TenantId}", state.TenantId);
            return StayWithError(state, AdminApiErrorMapper.Format(ex, EventMappingsMessages.SaveTriggerFailed));
        }
    }

    public async Task<AdminPageOutcome> DeleteTriggerAsync(
        EventMappingsWorkState state,
        CancellationToken cancellationToken = default)
    {
        var trigger = state.TriggerName?.Trim();
        var eventType = state.TriggerEventType?.Trim();

        if (string.IsNullOrWhiteSpace(trigger) || string.IsNullOrWhiteSpace(eventType))
            return Redirect(state, errorMessage: EventMappingsMessages.DeleteTriggerUnidentified);

        try
        {
            var root = await LoadCategoryRootAsync(state, CategoryEventTriggers, cancellationToken);
            if (root[trigger] is JsonArray bindings)
            {
                var remaining = new JsonArray();
                foreach (var binding in bindings)
                {
                    if (binding is JsonObject obj
                        && string.Equals(ReadBindingValue(obj, "eventType"), eventType, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    remaining.Add(binding?.DeepClone());
                }

                if (remaining.Count == 0)
                    root.Remove(trigger);
                else
                    root[trigger] = remaining;

                await UpsertCategoryAsync(state, CategoryEventTriggers, root, cancellationToken);
                await tenantAdminClient.RefreshTenantConfigurationAsync(cancellationToken);
            }

            return Redirect(
                state,
                EventMappingsMessages.RemovedTrigger(eventType, trigger),
                refreshLocalCaches: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete EventTriggers binding for {TenantId}", state.TenantId);
            return Redirect(
                state,
                errorMessage: AdminApiErrorMapper.Format(ex, EventMappingsMessages.DeleteTriggerFailed));
        }
    }

    public async Task<AdminPageOutcome> SaveMappingAsync(
        EventMappingsWorkState state,
        CancellationToken cancellationToken = default)
    {
        await LoadPageDataAsync(state, cancellationToken);
        await LoadSavedTypedMappingsAsync(state, cancellationToken);
        await LoadSavedTriggersAsync(state, cancellationToken);
        await LoadSelectedSchemaDefinitionAsync(state, cancellationToken);

        state.SelectedTemplateId = state.SelectedTemplateId?.Trim();
        state.SelectedEventType = state.SelectedEventType?.Trim();

        var errors = new List<FormValidationError>();
        if (string.IsNullOrWhiteSpace(state.SelectedTemplateId))
            errors.Add(new FormValidationError(nameof(EventMappingsWorkState.SelectedTemplateId), EventMappingsMessages.SelectTemplate));
        else if (!await IsTemplateAllowedForCurrentTenantAsync(state, state.SelectedTemplateId, cancellationToken))
        {
            errors.Add(new FormValidationError(
                nameof(EventMappingsWorkState.SelectedTemplateId),
                EventMappingsMessages.SelectTenantTemplate));
        }

        if (string.IsNullOrWhiteSpace(state.SelectedEventType))
            errors.Add(new FormValidationError(nameof(EventMappingsWorkState.SelectedEventType), EventMappingsMessages.SelectEventType));

        if (string.IsNullOrWhiteSpace(state.MappingJson))
            errors.Add(new FormValidationError(nameof(EventMappingsWorkState.MappingJson), EventMappingsMessages.EnterMappingJson));

        if (errors.Count > 0)
            return Stay(state, errors);

        EventFieldMapping? mapping;
        try
        {
            mapping = JsonSerializer.Deserialize<EventFieldMapping>(state.MappingJson!, JsonReadOptions);
        }
        catch (JsonException ex)
        {
            return Stay(state, [new FormValidationError(
                nameof(EventMappingsWorkState.MappingJson),
                EventMappingsMessages.InvalidJson(ex.Message))]);
        }

        if (mapping is null)
        {
            return Stay(state, [new FormValidationError(
                nameof(EventMappingsWorkState.MappingJson),
                EventMappingsMessages.MappingParseFailed)]);
        }

        if (string.IsNullOrWhiteSpace(mapping.MappingId))
            errors.Add(new FormValidationError(nameof(EventMappingsWorkState.MappingJson), EventMappingsMessages.MappingIdRequired));

        if (string.IsNullOrWhiteSpace(mapping.EventType))
            mapping.EventType = state.SelectedEventType!;
        else if (!string.Equals(mapping.EventType, state.SelectedEventType, StringComparison.OrdinalIgnoreCase))
            errors.Add(new FormValidationError(nameof(EventMappingsWorkState.MappingJson), EventMappingsMessages.EventTypeMustMatch));

        if (mapping.FieldMappings is null || mapping.FieldMappings.Count == 0)
            errors.Add(new FormValidationError(nameof(EventMappingsWorkState.MappingJson), EventMappingsMessages.FieldMappingsRequired));

        var catalogueItem = state.Catalogue.FirstOrDefault(c =>
            string.Equals(c.EventTypeName, state.SelectedEventType, StringComparison.OrdinalIgnoreCase));
        if (catalogueItem is { Kind: EventPublishKind.Typed, Properties.Count: > 0 }
            && mapping.FieldMappings is { Count: > 0 })
        {
            var known = catalogueItem.Properties.Select(p => p).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unknown = mapping.FieldMappings.Keys
                .Where(k => !known.Contains(k))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (unknown.Count > 0)
            {
                state.ValidationWarnings = unknown
                    .Select(k => EventMappingsMessages.UnknownProperty(k, state.SelectedEventType!))
                    .ToList();
            }
        }

        if (errors.Count > 0)
            return Stay(state, errors);

        try
        {
            var root = await LoadCategoryRootAsync(state, CategoryEventMappings, cancellationToken);
            var mappingJson = JsonSerializer.Serialize(mapping, JsonPersistOptions);
            var primaryTemplateKey = state.SelectedTemplateId!;
            var aliasKeys = await ResolveTemplateMappingKeysAsync(primaryTemplateKey, cancellationToken);

            var duplicateLocation = FindDuplicateMappingIdLocation(
                root,
                mapping.MappingId!,
                state.SelectedEventType!,
                primaryTemplateKey,
                aliasKeys);
            if (duplicateLocation is not null)
            {
                return Stay(state, [
                    new FormValidationError(
                        nameof(EventMappingsWorkState.MappingJson),
                        EventMappingsMessages.DuplicateMappingId(mapping.MappingId!, duplicateLocation))]);
            }

            var templateNode = root[primaryTemplateKey] as JsonObject ?? new JsonObject();
            root[primaryTemplateKey] = templateNode;

            var mappingNode = JsonNode.Parse(mappingJson)
                ?? throw new InvalidOperationException("Failed to serialise mapping.");
            templateNode[state.SelectedEventType!] = mappingNode;

            // Legacy saves duplicated alias keys (GUID + schema templateId). Keep one canonical row.
            foreach (var aliasKey in aliasKeys)
            {
                if (string.Equals(aliasKey, primaryTemplateKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (root[aliasKey] is not JsonObject aliasNode)
                    continue;

                aliasNode.Remove(state.SelectedEventType!);
                if (aliasNode.Count == 0)
                    root.Remove(aliasKey);
            }

            await UpsertCategoryAsync(state, CategoryEventMappings, root, cancellationToken);
            await tenantAdminClient.RefreshTenantConfigurationAsync(cancellationToken);

            return Redirect(
                state,
                EventMappingsMessages.SavedMapping(primaryTemplateKey, state.SelectedEventType!),
                refreshLocalCaches: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save EventMappings for {TenantId}", state.TenantId);
            return StayWithError(state, AdminApiErrorMapper.Format(ex, EventMappingsMessages.SaveMappingFailed));
        }
    }

    public async Task<AdminPageOutcome> SaveSchemaAsync(
        EventMappingsWorkState state,
        CancellationToken cancellationToken = default)
    {
        await LoadPageDataAsync(state, cancellationToken);
        await LoadSavedTypedMappingsAsync(state, cancellationToken);
        await LoadSavedTriggersAsync(state, cancellationToken);
        await LoadSelectedMappingAsync(state, cancellationToken);

        var schemaKey = (state.NewSchemaEventType ?? state.SelectedSchemaEventType)?.Trim();
        if (string.IsNullOrWhiteSpace(schemaKey))
        {
            return Stay(state, [new FormValidationError(
                nameof(EventMappingsWorkState.NewSchemaEventType),
                EventMappingsMessages.EnterSchemaEventType)]);
        }

        if (state.Catalogue.Any(c =>
                string.Equals(c.Kind, EventPublishKind.Typed, StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.EventTypeName, schemaKey, StringComparison.OrdinalIgnoreCase)))
        {
            return Stay(state, [new FormValidationError(
                nameof(EventMappingsWorkState.NewSchemaEventType),
                EventMappingsMessages.TypedEventNameClash(schemaKey))]);
        }

        if (string.IsNullOrWhiteSpace(state.SchemaDefinitionJson))
        {
            return Stay(state, [new FormValidationError(
                nameof(EventMappingsWorkState.SchemaDefinitionJson),
                EventMappingsMessages.EnterSchemaDefinitionJson)]);
        }

        JsonNode? definitionNode;
        try
        {
            definitionNode = JsonNode.Parse(state.SchemaDefinitionJson);
        }
        catch (JsonException ex)
        {
            return Stay(state, [new FormValidationError(
                nameof(EventMappingsWorkState.SchemaDefinitionJson),
                EventMappingsMessages.InvalidJson(ex.Message))]);
        }

        if (definitionNode is not JsonObject defObj)
        {
            return Stay(state, [new FormValidationError(
                nameof(EventMappingsWorkState.SchemaDefinitionJson),
                EventMappingsMessages.SchemaMustBeObject)]);
        }

        var errors = new List<FormValidationError>();
        if (defObj["topicName"] is null && defObj["TopicName"] is null)
            errors.Add(new FormValidationError(nameof(EventMappingsWorkState.SchemaDefinitionJson), EventMappingsMessages.TopicNameRequired));

        if (defObj["jsonSchema"] is null && defObj["JsonSchema"] is null)
            errors.Add(new FormValidationError(nameof(EventMappingsWorkState.SchemaDefinitionJson), EventMappingsMessages.JsonSchemaRequired));

        if (errors.Count > 0)
            return Stay(state, errors);

        try
        {
            var root = await LoadCategoryRootAsync(state, CategorySchemaEvents, cancellationToken);
            root[schemaKey] = definitionNode;
            await UpsertCategoryAsync(state, CategorySchemaEvents, root, cancellationToken);
            await tenantAdminClient.RefreshTenantConfigurationAsync(cancellationToken);

            state.SelectedEventType = schemaKey;
            state.SelectedSchemaEventType = schemaKey;
            return Redirect(
                state,
                EventMappingsMessages.SavedSchema(schemaKey),
                refreshLocalCaches: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save SchemaEvents for {TenantId}", state.TenantId);
            return StayWithError(state, AdminApiErrorMapper.Format(ex, EventMappingsMessages.SaveSchemaFailed));
        }
    }

    private async Task LoadPageDataAsync(EventMappingsWorkState state, CancellationToken cancellationToken)
    {
        await LoadCatalogueAsync(state, cancellationToken);
        await LoadTemplateOptionsAsync(state, cancellationToken);

        state.SchemaEvents = schemaEventDefinitionProvider.GetAll()
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new SchemaEventRow(
                kv.Key,
                kv.Value.TopicName,
                kv.Value.Version,
                kv.Value.Description))
            .ToList();

        var eventOptions = state.Catalogue
            .Select(e => new AdminSelectOption(
                $"{e.EventTypeName} ({e.Kind})",
                e.EventTypeName,
                string.Equals(e.EventTypeName, state.SelectedEventType, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var schema in state.SchemaEvents)
        {
            if (eventOptions.Any(o => string.Equals(o.Value, schema.MessageType, StringComparison.OrdinalIgnoreCase)))
                continue;
            eventOptions.Add(new AdminSelectOption(
                $"{schema.MessageType} (Schema)",
                schema.MessageType,
                string.Equals(schema.MessageType, state.SelectedEventType, StringComparison.OrdinalIgnoreCase)));
        }

        state.EventTypeOptions = eventOptions
            .OrderBy(o => o.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();

        state.TriggerOptions = TriggerNames
            .Select(t => new AdminSelectOption(t, t, string.Equals(t, state.TriggerName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        state.TriggerEventTypeOptions = state.EventTypeOptions
            .Where(o => !string.Equals(o.Value, SystemOnlyEventType, StringComparison.OrdinalIgnoreCase))
            .Select(o => new AdminSelectOption(
                o.Text,
                o.Value,
                string.Equals(o.Value, state.TriggerEventType, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!string.IsNullOrWhiteSpace(state.SelectedEventType))
        {
            var item = state.Catalogue.FirstOrDefault(c =>
                string.Equals(c.EventTypeName, state.SelectedEventType, StringComparison.OrdinalIgnoreCase));
            state.ClrPropertyHints = item?.Properties ?? [];
        }
    }

    private async Task LoadCatalogueAsync(EventMappingsWorkState state, CancellationToken cancellationToken)
    {
        try
        {
            var response = await tenantAdminClient.GetEventCatalogueAsync(cancellationToken);
            state.Catalogue = (response.Events ?? [])
                .Select(e => new EventCatalogueRow(
                    e.EventTypeName,
                    e.TopicName ?? "(no topic resolved)",
                    e.ClrTypeName,
                    e.Description,
                    e.Version,
                    string.IsNullOrWhiteSpace(e.Kind) ? EventPublishKind.Typed : e.Kind,
                    (e.Properties ?? []).Select(p => p.Name).ToList()))
                .ToList();
            state.CatalogueSource = "API";
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Event catalogue API unavailable; falling back to local registry.");
        }

        state.Catalogue = eventTypeRegistry.GetCatalogue()
            .Select(e => new EventCatalogueRow(
                e.EventTypeName,
                e.TopicName ?? "(no topic resolved)",
                e.ClrType.FullName ?? e.ClrType.Name,
                Description: null,
                Version: "local",
                Kind: EventPublishKind.Typed,
                Properties: e.ClrType.GetProperties().Select(p => p.Name).ToList()))
            .ToList();
        state.CatalogueSource = "local registry (API unavailable)";
    }

    private async Task LoadTemplateOptionsAsync(EventMappingsWorkState state, CancellationToken cancellationToken)
    {
        try
        {
            var templates = await templatesClient.GetAccessibleTemplatesAsync(cancellationToken) ?? [];
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            state.TemplateOptions = templates
                .Where(t => t.TemplateId != Guid.Empty)
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t =>
                {
                    var id = t.TemplateId.ToString();
                    allowed.Add(id);
                    var label = string.IsNullOrWhiteSpace(t.Name) ? id : $"{t.Name} ({id})";
                    return new AdminSelectOption(
                        label,
                        id,
                        string.Equals(id, state.SelectedTemplateId, StringComparison.OrdinalIgnoreCase));
                })
                .ToList();

            foreach (var template in templates.Where(t => t.TemplateId != Guid.Empty))
            {
                foreach (var key in await ResolveTemplateMappingKeysAsync(
                             template.TemplateId.ToString(),
                             cancellationToken))
                {
                    allowed.Add(key);
                }
            }

            state.AllowedTemplateKeys = allowed;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load templates for EventMappings editor");
            state.TemplateOptions = [];
            state.AllowedTemplateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<bool> IsTemplateAllowedForCurrentTenantAsync(
        EventMappingsWorkState state,
        string templateId,
        CancellationToken cancellationToken)
    {
        if (state.AllowedTemplateKeys.Count == 0)
            await LoadTemplateOptionsAsync(state, cancellationToken);

        return state.AllowedTemplateKeys.Contains(templateId);
    }

    private async Task<IReadOnlyList<string>> ResolveTemplateMappingKeysAsync(
        string selectedTemplateId,
        CancellationToken cancellationToken)
    {
        var keys = new List<string> { selectedTemplateId };

        if (!Guid.TryParse(selectedTemplateId, out var templateGuid))
            return keys;

        try
        {
            var schema = await templatesClient.GetLatestTemplateSchemaAsync(templateGuid, cancellationToken);
            if (string.IsNullOrWhiteSpace(schema?.JsonSchema))
                return keys;

            var schemaText = schema.JsonSchema.Trim();
            if (!schemaText.StartsWith('{') && !schemaText.StartsWith('['))
            {
                try
                {
                    schemaText = Encoding.UTF8.GetString(Convert.FromBase64String(schemaText));
                }
                catch (FormatException)
                {
                    return keys;
                }
            }

            using var doc = JsonDocument.Parse(schemaText);
            if (doc.RootElement.TryGetProperty("templateId", out var embeddedId)
                && embeddedId.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(embeddedId.GetString()))
            {
                var schemaTemplateId = embeddedId.GetString()!.Trim();
                if (!keys.Contains(schemaTemplateId, StringComparer.OrdinalIgnoreCase))
                    keys.Add(schemaTemplateId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not resolve schema templateId alias for EventMappings key {TemplateId}",
                selectedTemplateId);
        }

        return keys;
    }

    private async Task LoadSavedTypedMappingsAsync(EventMappingsWorkState state, CancellationToken cancellationToken)
    {
        try
        {
            var root = await LoadCategoryRootAsync(state, CategoryEventMappings, cancellationToken);
            var schemaNames = state.SchemaEvents
                .Select(s => s.MessageType)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var rows = new List<SavedMappingRow>();
            foreach (var templateProperty in root)
            {
                if (string.Equals(templateProperty.Key, "BasePath", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (templateProperty.Value is not JsonObject templateNode)
                    continue;

                foreach (var eventProperty in templateNode)
                {
                    if (eventProperty.Value is not JsonObject mappingNode)
                        continue;

                    var eventType = eventProperty.Key;
                    if (schemaNames.Contains(eventType))
                        continue;

                    var mappingId = mappingNode["mappingId"]?.GetValue<string>()
                        ?? mappingNode["MappingId"]?.GetValue<string>()
                        ?? "—";
                    var description = mappingNode["description"]?.GetValue<string>()
                        ?? mappingNode["Description"]?.GetValue<string>();

                    rows.Add(new SavedMappingRow(
                        templateProperty.Key,
                        eventType,
                        mappingId,
                        description));
                }
            }

            state.SavedTypedMappings = rows
                .Where(r => state.AllowedTemplateKeys.Count == 0 || state.AllowedTemplateKeys.Contains(r.TemplateId))
                .GroupBy(r => $"{r.EventType}|{r.MappingId}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g
                    .OrderBy(r => Guid.TryParse(r.TemplateId, out _) ? 0 : 1)
                    .ThenBy(r => r.TemplateId, StringComparer.OrdinalIgnoreCase)
                    .First())
                .OrderBy(r => r.EventType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.TemplateId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load saved typed EventMappings");
            state.SavedTypedMappings = [];
        }
    }

    private static string? FindDuplicateMappingIdLocation(
        JsonObject root,
        string mappingId,
        string eventType,
        string primaryTemplateKey,
        IReadOnlyList<string> aliasKeys)
    {
        foreach (var templateProperty in root)
        {
            if (string.Equals(templateProperty.Key, "BasePath", StringComparison.OrdinalIgnoreCase)
                || templateProperty.Value is not JsonObject templateNode)
            {
                continue;
            }

            foreach (var eventProperty in templateNode)
            {
                if (eventProperty.Value is not JsonObject mappingNode)
                    continue;

                var existingId = mappingNode["mappingId"]?.GetValue<string>()
                    ?? mappingNode["MappingId"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(existingId)
                    || !string.Equals(existingId, mappingId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var templateKey = templateProperty.Key;
                var existingEvent = eventProperty.Key;

                // Updating the same template + event (including alias keys) is allowed.
                if (string.Equals(existingEvent, eventType, StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(templateKey, primaryTemplateKey, StringComparison.OrdinalIgnoreCase)
                        || aliasKeys.Contains(templateKey, StringComparer.OrdinalIgnoreCase)))
                {
                    continue;
                }

                return $"template '{templateKey}' / {existingEvent}";
            }
        }

        return null;
    }

    private static JsonObject BuildBindingNode(string eventKind, string eventType, string mappingId) =>
        new()
        {
            ["eventKind"] = eventKind,
            ["eventType"] = eventType,
            ["mappingId"] = mappingId
        };

    private static string? ReadBindingValue(JsonObject binding, string camelCaseName)
    {
        var pascalCaseName = char.ToUpperInvariant(camelCaseName[0]) + camelCaseName[1..];
        var node = binding[camelCaseName] ?? binding[pascalCaseName];
        return node?.GetValue<string>();
    }

    private async Task LoadSavedTriggersAsync(EventMappingsWorkState state, CancellationToken cancellationToken)
    {
        try
        {
            var root = await LoadCategoryRootAsync(state, CategoryEventTriggers, cancellationToken);
            var rows = new List<TriggerBindingRow>();

            foreach (var triggerProperty in root)
            {
                if (triggerProperty.Value is not JsonArray bindings)
                    continue;

                foreach (var binding in bindings)
                {
                    if (binding is not JsonObject obj)
                        continue;

                    var eventType = ReadBindingValue(obj, "eventType");
                    if (string.IsNullOrWhiteSpace(eventType))
                        continue;

                    rows.Add(new TriggerBindingRow(
                        triggerProperty.Key,
                        ReadBindingValue(obj, "eventKind") ?? EventPublishKind.Typed,
                        eventType,
                        ReadBindingValue(obj, "mappingId") ?? "—"));
                }
            }

            state.SavedTriggers = rows
                .OrderBy(r => r.Trigger, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.EventType, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load saved EventTriggers");
            state.SavedTriggers = [];
        }
    }

    private async Task LoadSelectedSchemaDefinitionAsync(EventMappingsWorkState state, CancellationToken cancellationToken)
    {
        var key = state.SelectedSchemaEventType?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            if (string.IsNullOrWhiteSpace(state.SchemaDefinitionJson))
                state.SchemaDefinitionJson = GetEmptySchemaTemplate();
            return;
        }

        state.NewSchemaEventType = key;
        state.SelectedSchemaEventType = key;

        try
        {
            var root = await LoadCategoryRootAsync(state, CategorySchemaEvents, cancellationToken);
            JsonNode? definitionNode = null;
            foreach (var property in root)
            {
                if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    definitionNode = property.Value;
                    state.NewSchemaEventType = property.Key;
                    state.SelectedSchemaEventType = property.Key;
                    break;
                }
            }

            if (definitionNode is not null)
            {
                state.SchemaDefinitionJson = definitionNode.ToJsonString(JsonWriteOptions);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load SchemaEvents definition for {SchemaKey}", key);
        }

        var def = schemaEventDefinitionProvider.GetDefinition(key);
        if (def is not null)
        {
            state.SchemaDefinitionJson = JsonSerializer.Serialize(new
            {
                topicName = def.TopicName,
                version = def.Version,
                description = def.Description,
                jsonSchema = def.JsonSchema
                    ?? new Dictionary<string, object?> { ["type"] = "object", ["properties"] = new { } }
            }, JsonWriteOptions);
            return;
        }

        state.SchemaDefinitionJson = GetEmptySchemaTemplate();
    }

    private async Task LoadSelectedMappingAsync(EventMappingsWorkState state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.SelectedTemplateId) || string.IsNullOrWhiteSpace(state.SelectedEventType))
        {
            state.MappingJson = GetEmptyMappingTemplate(state.SelectedEventType);
            return;
        }

        try
        {
            var root = await LoadCategoryRootAsync(state, CategoryEventMappings, cancellationToken);
            var lookupKeys = await ResolveTemplateMappingKeysAsync(state.SelectedTemplateId, cancellationToken);
            foreach (var key in lookupKeys)
            {
                if (root[key] is JsonObject template
                    && template[state.SelectedEventType] is JsonNode mappingNode)
                {
                    state.MappingJson = mappingNode.ToJsonString(JsonWriteOptions);
                    return;
                }
            }

            foreach (var property in root)
            {
                if (property.Value is JsonObject template
                    && template[state.SelectedEventType] is JsonNode mappingNode)
                {
                    state.MappingJson = mappingNode.ToJsonString(JsonWriteOptions);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load existing EventMappings for editor");
        }

        state.MappingJson = GetEmptyMappingTemplate(state.SelectedEventType);
    }

    private async Task<JsonObject> LoadCategoryRootAsync(
        EventMappingsWorkState state,
        string category,
        CancellationToken cancellationToken)
    {
        var response = await tenantAdminClient.GetSafeTenantSettingsAsync(state.TenantId, cancellationToken);
        state.TenantName = response.TenantName ?? state.TenantName;

        var candidates = (response.Settings ?? [])
            .Where(s => string.Equals(s.Category, category, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var setting = candidates.FirstOrDefault(s => string.Equals(s.Target, TargetShared, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(s => string.Equals(s.Target, TargetWeb, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(setting?.SettingsJson))
            return new JsonObject();

        try
        {
            return JsonNode.Parse(setting.SettingsJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private async Task UpsertCategoryAsync(
        EventMappingsWorkState state,
        string category,
        JsonObject root,
        CancellationToken cancellationToken)
    {
        var payloadJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        await tenantAdminClient.UpsertSafeTenantSettingAsync(
            state.TenantId,
            new UpsertTenantSettingRequest(
                category,
                TargetShared,
                AdminSettingsEncoding.ToBase64(payloadJson),
                IsSecret: false),
            cancellationToken);
    }

    private static string GetEmptyMappingTemplate(string? eventType)
    {
        var mapping = new EventFieldMapping
        {
            MappingId = string.IsNullOrWhiteSpace(eventType)
                ? "mapping-v1"
                : $"{ToKebab(eventType)}-v1",
            EventType = eventType ?? string.Empty,
            Description = null,
            FieldMappings = new Dictionary<string, FieldMapping>()
        };

        return JsonSerializer.Serialize(mapping, JsonWriteOptions);
    }

    private static string GetEmptySchemaTemplate() =>
        JsonSerializer.Serialize(new
        {
            topicName = "my-custom-topic",
            version = "1.0",
            description = "Tenant-defined schema event",
            jsonSchema = new
            {
                type = "object",
                properties = new { }
            }
        }, JsonWriteOptions);

    private static string ToKebab(string value)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0)
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    private static AdminPageOutcome Stay(EventMappingsWorkState state, IReadOnlyList<FormValidationError> errors) =>
        AdminPageOutcome.Stay(errors: errors);

    private static AdminPageOutcome StayWithError(EventMappingsWorkState state, string errorMessage)
    {
        state.HasError = true;
        state.ErrorMessage = errorMessage;
        return AdminPageOutcome.Stay(errorMessage: errorMessage);
    }

    private static AdminPageOutcome Redirect(
        EventMappingsWorkState state,
        string? successMessage = null,
        string? errorMessage = null,
        bool refreshLocalCaches = false) =>
        AdminPageOutcome.Redirect(
            successMessage: successMessage,
            errorMessage: errorMessage,
            refreshLocalCaches: refreshLocalCaches,
            routeValues: new Dictionary<string, string?>
            {
                ["SelectedTemplateId"] = state.SelectedTemplateId,
                ["SelectedEventType"] = state.SelectedEventType,
                ["SelectedSchemaEventType"] = state.SelectedSchemaEventType
            });
}
