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
/// Tenant Admin editor for EventMappings + SchemaEvents, driven by the platform event catalogue API.
/// </summary>
[Authorize(Roles = AdminAccessHelper.AuthorizeRoles)]
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
    private const string TargetWeb = "Web";
    private const string CategoryEventMappings = "EventMappings";
    private const string CategorySchemaEvents = "SchemaEvents";

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
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

    public IReadOnlyList<string> ClrPropertyHints { get; private set; } = [];

    public IReadOnlyList<string> ValidationWarnings { get; private set; } = [];

    public string? CatalogueSource { get; private set; }

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
        await LoadSelectedMappingAsync(cancellationToken);
        LoadSelectedSchemaDefinition();
        return Page();
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
            LoadSelectedSchemaDefinition();
            return Page();
        }

        await LoadPageDataAsync(cancellationToken);
        LoadSelectedSchemaDefinition();

        SelectedTemplateId = SelectedTemplateId?.Trim();
        SelectedEventType = SelectedEventType?.Trim();

        if (string.IsNullOrWhiteSpace(SelectedTemplateId))
            ModelState.AddModelError(nameof(SelectedTemplateId), "Select a template.");

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
            var templateNode = root[SelectedTemplateId!] as JsonObject ?? new JsonObject();
            root[SelectedTemplateId!] = templateNode;

            var mappingNode = JsonNode.Parse(JsonSerializer.Serialize(mapping, JsonReadOptions))
                ?? throw new InvalidOperationException("Failed to serialise mapping.");
            templateNode[SelectedEventType!] = mappingNode;

            await UpsertCategoryAsync(CategoryEventMappings, root, cancellationToken);
            await RefreshCachesAsync(cancellationToken);

            TempData["EventMappingsSuccess"] =
                $"Saved mapping for template {SelectedTemplateId} / {SelectedEventType}.";
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
            await LoadSelectedMappingAsync(cancellationToken);
            return Page();
        }

        await LoadPageDataAsync(cancellationToken);
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
            TemplateOptions = templates
                .Where(t => t.TemplateId != Guid.Empty)
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t =>
                {
                    var id = t.TemplateId.ToString();
                    var label = string.IsNullOrWhiteSpace(t.Name) ? id : $"{t.Name} ({id})";
                    return new SelectListItem(
                        label,
                        id,
                        string.Equals(id, SelectedTemplateId, StringComparison.OrdinalIgnoreCase));
                })
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load templates for EventMappings editor");
            TemplateOptions = [];
        }
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
            if (root[SelectedTemplateId] is JsonObject template
                && template[SelectedEventType] is JsonNode mappingNode)
            {
                MappingJson = mappingNode.ToJsonString(JsonWriteOptions);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load existing EventMappings for editor");
        }

        MappingJson = GetEmptyMappingTemplate(SelectedEventType);
    }

    private void LoadSelectedSchemaDefinition()
    {
        var key = SelectedSchemaEventType ?? SelectedEventType;
        if (string.IsNullOrWhiteSpace(key))
        {
            SchemaDefinitionJson = GetEmptySchemaTemplate();
            return;
        }

        var def = schemaEventDefinitionProvider.GetDefinition(key);
        if (def is null)
        {
            SchemaDefinitionJson = GetEmptySchemaTemplate();
            return;
        }

        SchemaDefinitionJson = JsonSerializer.Serialize(new
        {
            topicName = def.TopicName,
            version = def.Version,
            description = def.Description,
            jsonSchema = def.JsonSchema ?? new Dictionary<string, object?> { ["type"] = "object", ["properties"] = new { } }
        }, JsonWriteOptions);
    }

    private async Task<JsonObject> LoadCategoryRootAsync(string category, CancellationToken cancellationToken)
    {
        var response = await tenantAdminClient.GetSafeTenantSettingsAsync(TenantId, cancellationToken);
        TenantName = response.TenantName ?? TenantName;

        var setting = response.Settings?
            .FirstOrDefault(s => string.Equals(s.Category, category, StringComparison.OrdinalIgnoreCase));

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
                TargetWeb,
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
}
