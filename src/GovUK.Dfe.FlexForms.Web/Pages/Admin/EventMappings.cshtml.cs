using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Domain.Models.EventMapping;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Current-tenant editor for EventMappings + SchemaEvents (Tenant Admin or SuperAdmin).
/// All reads/writes use the resolved request tenant; templates are limited to that tenant's catalogue.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageEventMappingsPolicy)]
public sealed class EventMappingsModel(
    ITenantAdminClient tenantAdminClient,
    ITenantRequestContext tenantRequestContext,
    ITenantConfigurationCache tenantConfigurationCache,
    ITenantIdResolver tenantIdResolver,
    ITemplatesClient templatesClient,
    IEventTypeRegistry eventTypeRegistry,
    ISchemaEventDefinitionProvider schemaEventDefinitionProvider,
    ILogger<EventMappingsModel> logger) : PageModel
{
    /// <summary>
    /// These categories are read by the API at runtime, so they are stored against the Shared
    /// target rather than the Web-only target.
    /// </summary>
    private const string TargetShared = "Shared";

    /// <summary>Pre-migration target; still read so existing tenants keep working until their next save.</summary>
    private const string TargetWeb = "Web";

    private const string CategoryEventMappings = "EventMappings";
    private const string CategorySchemaEvents = "SchemaEvents";
    private const string CategoryEventTriggers = "EventTriggers";

    /// <summary>Published by the API for every upload; never bindable as a tenant trigger.</summary>
    private const string SystemOnlyEventType = "ScanRequestedEvent";

    private static readonly string[] TriggerNames = ["ApplicationSubmitted", "FileUploaded"];

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

    /// <summary>Used when persisting mappings so null optional properties do not break API config flatten/round-trip.</summary>
    private static readonly JsonSerializerOptions JsonPersistOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public Guid TenantId { get; private set; }

    public string TenantName { get; private set; } = string.Empty;

    public bool HasError { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool ShowSuccess { get; private set; }

    public string? SuccessMessage { get; private set; }

    public IReadOnlyList<SelectListItem> TemplateOptions { get; private set; } = [];

    public IReadOnlyList<SelectListItem> EventTypeOptions { get; private set; } = [];

    public IReadOnlyList<EventCatalogueRow> Catalogue { get; private set; } = [];

    public IReadOnlyList<SchemaEventRow> SchemaEvents { get; private set; } = [];

    public IReadOnlyList<SavedMappingRow> SavedTypedMappings { get; private set; } = [];

    public IReadOnlyList<SelectListItem> TriggerOptions { get; private set; } = [];

    /// <summary>Event types selectable as a trigger binding (system-only events excluded).</summary>
    public IReadOnlyList<SelectListItem> TriggerEventTypeOptions { get; private set; } = [];

    public IReadOnlyList<TriggerBindingRow> SavedTriggers { get; private set; } = [];

    /// <summary>
    /// Template keys belonging to the current tenant (API GUIDs plus schema-embedded ids such as form-001).
    /// </summary>
    public IReadOnlySet<string> AllowedTemplateKeys { get; private set; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> ClrPropertyHints { get; private set; } = [];

    public IReadOnlyList<string> ValidationWarnings { get; private set; } = [];

    /// <summary>
    /// Platform Metadata keys (mirrors API PlatformEventMetadataKeys) for Admin guidance.
    /// </summary>
    public IReadOnlyList<MetadataKeyHint> FileUploadedMetadataHints { get; } =
    [
        new("applicationId", "Application id (GUID)"),
        new("applicationReference", "Human-readable application reference"),
        new("fileId", "Uploaded file id (GUID)"),
        new("fileName", "Stored / hashed file name"),
        new("originalFileName", "Original file name as uploaded"),
        new("filePath", "Storage path (without SAS)"),
        new("fileUri", "Read URI including short-lived SAS (or local file:// in development)"),
        new("fileHash", "Content hash used for scanning"),
        new("fileSize", "File size in bytes"),
        new("uploaderUserId", "User id of the uploader"),
        new("uploaderEmail", "Email of the uploader when known"),
        new("uploadedOn", "UTC timestamp when the file was uploaded")
    ];

    public IReadOnlyList<MetadataKeyHint> ApplicationSubmittedMetadataHints { get; } =
    [
        new("applicationId", "Application id (GUID)"),
        new("applicationReference", "Human-readable application reference"),
        new("submittedByUserId", "User id of the submitter"),
        new("submittedByEmail", "Email of the submitter"),
        new("submittedByFullName", "Full name of the submitter"),
        new("submittedOn", "UTC timestamp when the application was submitted")
    ];

    public string? CatalogueSource { get; private set; }

    public bool IsEditingSchemaEvent => !string.IsNullOrWhiteSpace(SelectedSchemaEventType);

    [BindProperty(SupportsGet = true)]
    public string? SelectedTemplateId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedEventType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedSchemaEventType { get; set; }

    [BindProperty]
    public string? MappingJson { get; set; }

    [BindProperty]
    public string? SchemaDefinitionJson { get; set; }

    [BindProperty]
    public string? NewSchemaEventType { get; set; }

    [BindProperty]
    public string? TriggerName { get; set; }

    [BindProperty]
    public string? TriggerEventKind { get; set; }

    [BindProperty]
    public string? TriggerEventType { get; set; }

    [BindProperty]
    public string? TriggerMappingId { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        ApplyTempData();
        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            return Page();
        }

        await LoadPageDataAsync(cancellationToken);
        await LoadSavedTypedMappingsAsync(cancellationToken);
        await LoadSavedTriggersAsync(cancellationToken);
        await LoadSelectedMappingAsync(cancellationToken);
        await LoadSelectedSchemaDefinitionAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveTriggerAsync(CancellationToken cancellationToken)
    {
        // Ignore the mapping/schema editors when saving a trigger binding.
        ModelState.Remove(nameof(MappingJson));
        ModelState.Remove(nameof(SchemaDefinitionJson));
        ModelState.Remove(nameof(NewSchemaEventType));

        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            return Page();
        }

        await LoadPageDataAsync(cancellationToken);
        await LoadSavedTypedMappingsAsync(cancellationToken);
        await LoadSavedTriggersAsync(cancellationToken);
        await LoadSelectedMappingAsync(cancellationToken);
        await LoadSelectedSchemaDefinitionAsync(cancellationToken);

        var trigger = TriggerName?.Trim();
        var eventType = TriggerEventType?.Trim();
        var mappingId = TriggerMappingId?.Trim();
        var eventKind = string.IsNullOrWhiteSpace(TriggerEventKind)
            ? EventPublishKind.Typed
            : TriggerEventKind.Trim();

        if (string.IsNullOrWhiteSpace(trigger)
            || !TriggerNames.Contains(trigger, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(TriggerName), "Select a trigger.");
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            ModelState.AddModelError(nameof(TriggerEventType), "Select an event type.");
        }
        else if (string.Equals(eventType, SystemOnlyEventType, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                nameof(TriggerEventType),
                $"{SystemOnlyEventType} is published by the platform for every upload and cannot be configured here.");
        }

        if (string.IsNullOrWhiteSpace(mappingId))
            ModelState.AddModelError(nameof(TriggerMappingId), "Enter the mapping ID to use.");

        if (!string.Equals(eventKind, EventPublishKind.Typed, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(eventKind, EventPublishKind.Schema, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(TriggerEventKind), "Event kind must be Typed or Schema.");
        }

        if (!ModelState.IsValid)
            return Page();

        try
        {
            var root = await LoadCategoryRootAsync(CategoryEventTriggers, cancellationToken);
            var bindings = root[trigger!] as JsonArray ?? new JsonArray();

            // One binding per event type per trigger: saving the same event type replaces it.
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

            await UpsertCategoryAsync(CategoryEventTriggers, root, cancellationToken);
            await RefreshCachesAsync(cancellationToken);

            TempData["EventMappingsSuccess"] =
                $"Saved {eventType} on the {trigger} trigger.";
            return RedirectToPage(new { SelectedTemplateId, SelectedEventType, SelectedSchemaEventType });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save EventTriggers for {TenantId}", TenantId);
            HasError = true;
            ErrorMessage = GetErrorMessage(ex, "Could not save event trigger.");
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteTriggerAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();

        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            return Page();
        }

        var trigger = TriggerName?.Trim();
        var eventType = TriggerEventType?.Trim();

        if (string.IsNullOrWhiteSpace(trigger) || string.IsNullOrWhiteSpace(eventType))
        {
            TempData["EventMappingsError"] = "Could not identify the trigger binding to remove.";
            return RedirectToPage(new { SelectedTemplateId, SelectedEventType, SelectedSchemaEventType });
        }

        try
        {
            var root = await LoadCategoryRootAsync(CategoryEventTriggers, cancellationToken);
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

                await UpsertCategoryAsync(CategoryEventTriggers, root, cancellationToken);
                await RefreshCachesAsync(cancellationToken);
            }

            TempData["EventMappingsSuccess"] = $"Removed {eventType} from the {trigger} trigger.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete EventTriggers binding for {TenantId}", TenantId);
            TempData["EventMappingsError"] = GetErrorMessage(ex, "Could not remove event trigger.");
        }

        return RedirectToPage(new { SelectedTemplateId, SelectedEventType, SelectedSchemaEventType });
    }

    public async Task<IActionResult> OnPostSaveMappingAsync(CancellationToken cancellationToken)
    {
        // Ignore schema-form fields when saving a typed/schema field mapping.
        ModelState.Remove(nameof(SchemaDefinitionJson));
        ModelState.Remove(nameof(NewSchemaEventType));

        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            await LoadPageDataAsync(cancellationToken);
            await LoadSavedTypedMappingsAsync(cancellationToken);
            await LoadSavedTriggersAsync(cancellationToken);
            await LoadSelectedSchemaDefinitionAsync(cancellationToken);
            return Page();
        }

        await LoadPageDataAsync(cancellationToken);
        await LoadSavedTypedMappingsAsync(cancellationToken);
        await LoadSavedTriggersAsync(cancellationToken);
        await LoadSelectedSchemaDefinitionAsync(cancellationToken);

        SelectedTemplateId = SelectedTemplateId?.Trim();
        SelectedEventType = SelectedEventType?.Trim();

        if (string.IsNullOrWhiteSpace(SelectedTemplateId))
            ModelState.AddModelError(nameof(SelectedTemplateId), "Select a template.");
        else if (!await IsTemplateAllowedForCurrentTenantAsync(SelectedTemplateId, cancellationToken))
        {
            ModelState.AddModelError(
                nameof(SelectedTemplateId),
                "Select a template that belongs to this tenant.");
        }

        if (string.IsNullOrWhiteSpace(SelectedEventType))
            ModelState.AddModelError(nameof(SelectedEventType), "Select an event type.");

        if (string.IsNullOrWhiteSpace(MappingJson))
            ModelState.AddModelError(nameof(MappingJson), "Enter mapping JSON.");

        if (!ModelState.IsValid)
            return Page();

        EventFieldMapping? mapping;
        try
        {
            mapping = JsonSerializer.Deserialize<EventFieldMapping>(MappingJson!, JsonReadOptions);
        }
        catch (JsonException ex)
        {
            ModelState.AddModelError(nameof(MappingJson), $"Invalid JSON: {ex.Message}");
            return Page();
        }

        if (mapping is null)
        {
            ModelState.AddModelError(nameof(MappingJson), "Mapping JSON could not be parsed.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(mapping.MappingId))
            ModelState.AddModelError(nameof(MappingJson), "mappingId is required.");

        if (string.IsNullOrWhiteSpace(mapping.EventType))
            mapping.EventType = SelectedEventType!;
        else if (!string.Equals(mapping.EventType, SelectedEventType, StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(MappingJson), "eventType in JSON must match the selected event type.");

        if (mapping.FieldMappings is null || mapping.FieldMappings.Count == 0)
            ModelState.AddModelError(nameof(MappingJson), "fieldMappings must contain at least one property.");

        var catalogueItem = Catalogue.FirstOrDefault(c =>
            string.Equals(c.EventTypeName, SelectedEventType, StringComparison.OrdinalIgnoreCase));
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
                ValidationWarnings = unknown
                    .Select(k => $"Property '{k}' is not on {SelectedEventType}.")
                    .ToList();
            }
        }

        if (!ModelState.IsValid)
            return Page();

        try
        {
            var root = await LoadCategoryRootAsync(CategoryEventMappings, cancellationToken);
            var mappingJson = JsonSerializer.Serialize(mapping, JsonPersistOptions);
            var templateKeys = await ResolveTemplateMappingKeysAsync(SelectedTemplateId!, cancellationToken);

            foreach (var templateKey in templateKeys)
            {
                var templateNode = root[templateKey] as JsonObject ?? new JsonObject();
                root[templateKey] = templateNode;

                var mappingNode = JsonNode.Parse(mappingJson)
                    ?? throw new InvalidOperationException("Failed to serialise mapping.");
                templateNode[SelectedEventType!] = mappingNode;
            }

            await UpsertCategoryAsync(CategoryEventMappings, root, cancellationToken);
            await RefreshCachesAsync(cancellationToken);

            var keysLabel = string.Join(", ", templateKeys);
            TempData["EventMappingsSuccess"] =
                $"Saved mapping for template key(s) [{keysLabel}] / {SelectedEventType}.";
            return RedirectToPage(new { SelectedTemplateId, SelectedEventType, SelectedSchemaEventType });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save EventMappings for {TenantId}", TenantId);
            HasError = true;
            ErrorMessage = GetErrorMessage(ex, "Could not save event mapping.");
            return Page();
        }
    }

    public async Task<IActionResult> OnPostSaveSchemaAsync(CancellationToken cancellationToken)
    {
        // Ignore mapping-form fields when saving a schema event definition.
        ModelState.Remove(nameof(MappingJson));

        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            await LoadPageDataAsync(cancellationToken);
            await LoadSavedTypedMappingsAsync(cancellationToken);
            await LoadSavedTriggersAsync(cancellationToken);
            await LoadSelectedMappingAsync(cancellationToken);
            return Page();
        }

        await LoadPageDataAsync(cancellationToken);
        await LoadSavedTypedMappingsAsync(cancellationToken);
        await LoadSavedTriggersAsync(cancellationToken);
        await LoadSelectedMappingAsync(cancellationToken);

        var schemaKey = (NewSchemaEventType ?? SelectedSchemaEventType)?.Trim();
        if (string.IsNullOrWhiteSpace(schemaKey))
        {
            ModelState.AddModelError(nameof(NewSchemaEventType), "Enter a schema event type name.");
            return Page();
        }

        if (Catalogue.Any(c =>
                string.Equals(c.Kind, EventPublishKind.Typed, StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.EventTypeName, schemaKey, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(
                nameof(NewSchemaEventType),
                $"'{schemaKey}' is a platform typed event. Choose a different name for schema events.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(SchemaDefinitionJson))
        {
            ModelState.AddModelError(nameof(SchemaDefinitionJson), "Enter schema definition JSON.");
            return Page();
        }

        JsonNode? definitionNode;
        try
        {
            definitionNode = JsonNode.Parse(SchemaDefinitionJson);
        }
        catch (JsonException ex)
        {
            ModelState.AddModelError(nameof(SchemaDefinitionJson), $"Invalid JSON: {ex.Message}");
            return Page();
        }

        if (definitionNode is not JsonObject defObj)
        {
            ModelState.AddModelError(nameof(SchemaDefinitionJson), "Schema definition must be a JSON object.");
            return Page();
        }

        if (defObj["topicName"] is null && defObj["TopicName"] is null)
            ModelState.AddModelError(nameof(SchemaDefinitionJson), "topicName is required.");

        if (defObj["jsonSchema"] is null && defObj["JsonSchema"] is null)
            ModelState.AddModelError(nameof(SchemaDefinitionJson), "jsonSchema is required.");

        if (!ModelState.IsValid)
            return Page();

        try
        {
            var root = await LoadCategoryRootAsync(CategorySchemaEvents, cancellationToken);
            root[schemaKey] = definitionNode;
            await UpsertCategoryAsync(CategorySchemaEvents, root, cancellationToken);
            await RefreshCachesAsync(cancellationToken);

            TempData["EventMappingsSuccess"] = $"Saved schema event '{schemaKey}'.";
            return RedirectToPage(new
            {
                SelectedTemplateId,
                SelectedEventType = schemaKey,
                SelectedSchemaEventType = schemaKey
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save SchemaEvents for {TenantId}", TenantId);
            HasError = true;
            ErrorMessage = GetErrorMessage(ex, "Could not save schema event.");
            return Page();
        }
    }

    private async Task LoadPageDataAsync(CancellationToken cancellationToken)
    {
        await LoadCatalogueAsync(cancellationToken);
        await LoadTemplateOptionsAsync(cancellationToken);

        SchemaEvents = schemaEventDefinitionProvider.GetAll()
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new SchemaEventRow(
                kv.Key,
                kv.Value.TopicName,
                kv.Value.Version,
                kv.Value.Description))
            .ToList();

        var eventOptions = Catalogue
            .Select(e => new SelectListItem(
                $"{e.EventTypeName} ({e.Kind})",
                e.EventTypeName,
                string.Equals(e.EventTypeName, SelectedEventType, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var schema in SchemaEvents)
        {
            if (eventOptions.Any(o => string.Equals(o.Value, schema.MessageType, StringComparison.OrdinalIgnoreCase)))
                continue;
            eventOptions.Add(new SelectListItem(
                $"{schema.MessageType} (Schema)",
                schema.MessageType,
                string.Equals(schema.MessageType, SelectedEventType, StringComparison.OrdinalIgnoreCase)));
        }

        EventTypeOptions = eventOptions
            .OrderBy(o => o.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();

        TriggerOptions = TriggerNames
            .Select(t => new SelectListItem(t, t, string.Equals(t, TriggerName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        TriggerEventTypeOptions = EventTypeOptions
            .Where(o => !string.Equals(o.Value, SystemOnlyEventType, StringComparison.OrdinalIgnoreCase))
            .Select(o => new SelectListItem(
                o.Text,
                o.Value,
                string.Equals(o.Value, TriggerEventType, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!string.IsNullOrWhiteSpace(SelectedEventType))
        {
            var item = Catalogue.FirstOrDefault(c =>
                string.Equals(c.EventTypeName, SelectedEventType, StringComparison.OrdinalIgnoreCase));
            ClrPropertyHints = item?.Properties ?? [];
        }
    }

    private async Task LoadCatalogueAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await tenantAdminClient.GetEventCatalogueAsync(cancellationToken);
            Catalogue = (response.Events ?? [])
                .Select(e => new EventCatalogueRow(
                    e.EventTypeName,
                    e.TopicName ?? "(no topic resolved)",
                    e.ClrTypeName,
                    e.Description,
                    e.Version,
                    string.IsNullOrWhiteSpace(e.Kind) ? EventPublishKind.Typed : e.Kind,
                    (e.Properties ?? []).Select(p => p.Name).ToList()))
                .ToList();
            CatalogueSource = "API";
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Event catalogue API unavailable; falling back to local registry.");
        }

        Catalogue = eventTypeRegistry.GetCatalogue()
            .Select(e => new EventCatalogueRow(
                e.EventTypeName,
                e.TopicName ?? "(no topic resolved)",
                e.ClrType.FullName ?? e.ClrType.Name,
                Description: null,
                Version: "local",
                Kind: EventPublishKind.Typed,
                Properties: e.ClrType.GetProperties().Select(p => p.Name).ToList()))
            .ToList();
        CatalogueSource = "local registry (API unavailable)";
    }

    private async Task LoadTemplateOptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var templates = await templatesClient.GetAccessibleTemplatesAsync(cancellationToken) ?? [];
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            TemplateOptions = templates
                .Where(t => t.TemplateId != Guid.Empty)
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t =>
                {
                    var id = t.TemplateId.ToString();
                    allowed.Add(id);
                    var label = string.IsNullOrWhiteSpace(t.Name) ? id : $"{t.Name} ({id})";
                    return new SelectListItem(
                        label,
                        id,
                        string.Equals(id, SelectedTemplateId, StringComparison.OrdinalIgnoreCase));
                })
                .ToList();

            // Include schema-embedded template ids (e.g. form-001) for this tenant only.
            foreach (var template in templates.Where(t => t.TemplateId != Guid.Empty))
            {
                foreach (var key in await ResolveTemplateMappingKeysAsync(
                             template.TemplateId.ToString(),
                             cancellationToken))
                {
                    allowed.Add(key);
                }
            }

            AllowedTemplateKeys = allowed;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load templates for EventMappings editor");
            TemplateOptions = [];
            AllowedTemplateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<bool> IsTemplateAllowedForCurrentTenantAsync(
        string templateId,
        CancellationToken cancellationToken)
    {
        if (AllowedTemplateKeys.Count == 0)
            await LoadTemplateOptionsAsync(cancellationToken);

        return AllowedTemplateKeys.Contains(templateId);
    }

    /// <summary>
    /// Returns TenantConfig keys to write the mapping under: the selected key plus the
    /// schema-embedded <c>templateId</c> (e.g. form-001) when the selection is an API GUID.
    /// Submissions look up by the schema TemplateId, so both must exist for SaaS.
    /// </summary>
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

            // JsonSchema may be raw JSON or base64-encoded JSON depending on store.
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

    private async Task LoadSavedTypedMappingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var root = await LoadCategoryRootAsync(CategoryEventMappings, cancellationToken);
            var schemaNames = SchemaEvents
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

            SavedTypedMappings = rows
                .Where(r => AllowedTemplateKeys.Count == 0 || AllowedTemplateKeys.Contains(r.TemplateId))
                .GroupBy(
                    r => $"{r.TemplateId}|{r.EventType}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(r => r.EventType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.TemplateId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load saved typed EventMappings");
            SavedTypedMappings = [];
        }
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

    private async Task LoadSavedTriggersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var root = await LoadCategoryRootAsync(CategoryEventTriggers, cancellationToken);
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

            SavedTriggers = rows
                .OrderBy(r => r.Trigger, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.EventType, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load saved EventTriggers");
            SavedTriggers = [];
        }
    }

    private async Task LoadSelectedSchemaDefinitionAsync(CancellationToken cancellationToken)
    {
        var key = SelectedSchemaEventType?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            if (string.IsNullOrWhiteSpace(SchemaDefinitionJson))
                SchemaDefinitionJson = GetEmptySchemaTemplate();
            return;
        }

        // Replace editor contents with the saved definition (edit = replace).
        NewSchemaEventType = key;
        SelectedSchemaEventType = key;

        try
        {
            var root = await LoadCategoryRootAsync(CategorySchemaEvents, cancellationToken);
            JsonNode? definitionNode = null;
            foreach (var property in root)
            {
                if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    definitionNode = property.Value;
                    NewSchemaEventType = property.Key;
                    SelectedSchemaEventType = property.Key;
                    break;
                }
            }

            if (definitionNode is not null)
            {
                SchemaDefinitionJson = definitionNode.ToJsonString(JsonWriteOptions);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load SchemaEvents definition for {SchemaKey}", key);
        }

        // Provider fallback (request-scoped overlay).
        var def = schemaEventDefinitionProvider.GetDefinition(key);
        if (def is not null)
        {
            SchemaDefinitionJson = JsonSerializer.Serialize(new
            {
                topicName = def.TopicName,
                version = def.Version,
                description = def.Description,
                jsonSchema = def.JsonSchema
                    ?? new Dictionary<string, object?> { ["type"] = "object", ["properties"] = new { } }
            }, JsonWriteOptions);
            return;
        }

        SchemaDefinitionJson = GetEmptySchemaTemplate();
    }

    private async Task LoadSelectedMappingAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SelectedTemplateId) || string.IsNullOrWhiteSpace(SelectedEventType))
        {
            MappingJson = GetEmptyMappingTemplate(SelectedEventType);
            return;
        }

        try
        {
            var root = await LoadCategoryRootAsync(CategoryEventMappings, cancellationToken);
            var lookupKeys = await ResolveTemplateMappingKeysAsync(SelectedTemplateId, cancellationToken);
            foreach (var key in lookupKeys)
            {
                if (root[key] is JsonObject template
                    && template[SelectedEventType] is JsonNode mappingNode)
                {
                    MappingJson = mappingNode.ToJsonString(JsonWriteOptions);
                    return;
                }
            }

            // Fall back: any template key that already has this event mapping.
            foreach (var property in root)
            {
                if (property.Value is JsonObject template
                    && template[SelectedEventType] is JsonNode mappingNode)
                {
                    MappingJson = mappingNode.ToJsonString(JsonWriteOptions);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load existing EventMappings for editor");
        }

        MappingJson = GetEmptyMappingTemplate(SelectedEventType);
    }

    private async Task<JsonObject> LoadCategoryRootAsync(string category, CancellationToken cancellationToken)
    {
        var response = await tenantAdminClient.GetSafeTenantSettingsAsync(TenantId, cancellationToken);
        TenantName = response.TenantName ?? TenantName;

        var candidates = (response.Settings ?? [])
            .Where(s => string.Equals(s.Category, category, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Shared is where the API reads from; a leftover Web row is only a migration fallback.
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

    private async Task UpsertCategoryAsync(string category, JsonObject root, CancellationToken cancellationToken)
    {
        var payloadJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        await tenantAdminClient.UpsertSafeTenantSettingAsync(
            TenantId,
            new UpsertTenantSettingRequest(
                category,
                TargetShared,
                ToBase64SettingsJson(payloadJson),
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

    private async Task RefreshCachesAsync(CancellationToken cancellationToken)
    {
        await tenantAdminClient.RefreshTenantConfigurationAsync(cancellationToken);
        tenantConfigurationCache.Invalidate(TenantId);
        tenantIdResolver.InvalidateHostnameCache();
    }

    private bool TryResolveTenant(out string? error)
    {
        if (tenantRequestContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            error = "Tenant context is not available for this request.";
            return false;
        }

        TenantId = tenantId;
        TenantName = tenantRequestContext.TenantName ?? string.Empty;
        error = null;
        return true;
    }

    private void ApplyTempData()
    {
        if (TempData["EventMappingsSuccess"] is string success)
        {
            ShowSuccess = true;
            SuccessMessage = success;
        }

        if (TempData["EventMappingsError"] is string err)
        {
            HasError = true;
            ErrorMessage = err;
        }
    }

    private static string ToBase64SettingsJson(string settingsJson) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(settingsJson));

    private static string GetErrorMessage(Exception ex, string fallback)
    {
        if (ex is ExternalApplicationsException<ExceptionResponse> apiEx
            && !string.IsNullOrWhiteSpace(apiEx.Result?.Message))
        {
            return apiEx.Result.Message;
        }

        if (ex is ExternalApplicationsException clientEx && clientEx.StatusCode > 0)
            return $"{fallback} (HTTP {clientEx.StatusCode})";

        return fallback;
    }

    public sealed record EventCatalogueRow(
        string EventTypeName,
        string TopicName,
        string ClrTypeName,
        string? Description,
        string Version,
        string Kind,
        IReadOnlyList<string> Properties);

    public sealed record SchemaEventRow(
        string MessageType,
        string TopicName,
        string Version,
        string? Description);

    public sealed record SavedMappingRow(
        string TemplateId,
        string EventType,
        string MappingId,
        string? Description);

    public sealed record TriggerBindingRow(
        string Trigger,
        string EventKind,
        string EventType,
        string MappingId);

    public sealed record MetadataKeyHint(string Key, string Description);
}
