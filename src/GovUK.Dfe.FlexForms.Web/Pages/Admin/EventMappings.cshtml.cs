using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
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
/// Tenant Admin editor for EventMappings: platform event catalogue + per-template mapping JSON.
/// </summary>
[Authorize(Roles = AdminAccessHelper.AuthorizeRoles)]
public sealed class EventMappingsModel(
    ITenantAdminClient tenantAdminClient,
    ITenantRequestContext tenantRequestContext,
    ITenantConfigurationCache tenantConfigurationCache,
    ITenantIdResolver tenantIdResolver,
    ITemplatesClient templatesClient,
    IEventTypeRegistry eventTypeRegistry,
    ILogger<EventMappingsModel> logger) : PageModel
{
    private const string TargetWeb = "Web";
    private const string CategoryEventMappings = "EventMappings";

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

    public IReadOnlyList<string> ClrPropertyHints { get; private set; } = [];

    public IReadOnlyList<string> ValidationWarnings { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? SelectedTemplateId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedEventType { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Enter mapping JSON")]
    public string MappingJson { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        ApplyTempData();
        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            return Page();
        }

        await LoadCatalogueAndOptionsAsync(cancellationToken);
        await LoadSelectedMappingAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            await LoadCatalogueAndOptionsAsync(cancellationToken);
            return Page();
        }

        await LoadCatalogueAndOptionsAsync(cancellationToken);

        SelectedTemplateId = SelectedTemplateId?.Trim();
        SelectedEventType = SelectedEventType?.Trim();

        if (string.IsNullOrWhiteSpace(SelectedTemplateId))
            ModelState.AddModelError(nameof(SelectedTemplateId), "Select a template.");

        if (string.IsNullOrWhiteSpace(SelectedEventType))
            ModelState.AddModelError(nameof(SelectedEventType), "Select an event type.");

        if (!ModelState.IsValid)
            return Page();

        EventFieldMapping? mapping;
        try
        {
            mapping = JsonSerializer.Deserialize<EventFieldMapping>(MappingJson, JsonReadOptions);
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

        var clrType = eventTypeRegistry.GetEventType(SelectedEventType!);
        if (clrType != null && mapping.FieldMappings is { Count: > 0 })
        {
            var known = GetClrPropertyNames(clrType);
            var unknown = mapping.FieldMappings.Keys
                .Where(k => !known.Contains(k))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unknown.Count > 0)
            {
                ValidationWarnings = unknown
                    .Select(k => $"Property '{k}' is not on {clrType.Name}.")
                    .ToList();
            }
        }

        if (!ModelState.IsValid)
            return Page();

        try
        {
            var root = await LoadEventMappingsRootAsync(cancellationToken);
            var templateNode = root[SelectedTemplateId!] as JsonObject ?? new JsonObject();
            root[SelectedTemplateId!] = templateNode;

            // Store the mapping object (not a string) so FlattenJson nests correctly.
            var mappingNode = JsonNode.Parse(JsonSerializer.Serialize(mapping, JsonReadOptions))
                ?? throw new InvalidOperationException("Failed to serialise mapping.");
            templateNode[SelectedEventType!] = mappingNode;

            var payloadJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            await tenantAdminClient.UpsertSafeTenantSettingAsync(
                TenantId,
                new UpsertTenantSettingRequest(
                    CategoryEventMappings,
                    TargetWeb,
                    ToBase64SettingsJson(payloadJson),
                    IsSecret: false),
                cancellationToken);

            await RefreshCachesAsync(cancellationToken);

            TempData["EventMappingsSuccess"] =
                $"Saved mapping for template {SelectedTemplateId} / {SelectedEventType}.";
            return RedirectToPage(new { SelectedTemplateId, SelectedEventType });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save EventMappings for {TenantId}", TenantId);
            HasError = true;
            ErrorMessage = GetErrorMessage(ex, "Could not save event mapping.");
            return Page();
        }
    }

    private async Task LoadCatalogueAndOptionsAsync(CancellationToken cancellationToken)
    {
        Catalogue = eventTypeRegistry.GetCatalogue()
            .Select(e => new EventCatalogueRow(e.EventTypeName, e.TopicName ?? "(no topic resolved)", e.ClrType.FullName ?? e.ClrType.Name))
            .ToList();

        EventTypeOptions = Catalogue
            .Select(e => new SelectListItem(e.EventTypeName, e.EventTypeName, string.Equals(e.EventTypeName, SelectedEventType, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        try
        {
            var templates = await templatesClient.GetAccessibleTemplatesAsync(cancellationToken)
                ?? [];
            TemplateOptions = templates
                .Where(t => t.TemplateId != Guid.Empty)
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t =>
                {
                    var id = t.TemplateId.ToString();
                    var label = string.IsNullOrWhiteSpace(t.Name) ? id : $"{t.Name} ({id})";
                    return new SelectListItem(label, id, string.Equals(id, SelectedTemplateId, StringComparison.OrdinalIgnoreCase));
                })
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load templates for EventMappings editor");
            TemplateOptions = [];
        }

        if (!string.IsNullOrWhiteSpace(SelectedEventType))
        {
            var type = eventTypeRegistry.GetEventType(SelectedEventType);
            if (type != null)
                ClrPropertyHints = GetClrPropertyNames(type).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
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
            var root = await LoadEventMappingsRootAsync(cancellationToken);
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

    private async Task<JsonObject> LoadEventMappingsRootAsync(CancellationToken cancellationToken)
    {
        var response = await tenantAdminClient.GetSafeTenantSettingsAsync(TenantId, cancellationToken);
        TenantName = response.TenantName ?? TenantName;

        var setting = response.Settings?
            .FirstOrDefault(s => string.Equals(s.Category, CategoryEventMappings, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(setting?.SettingsJson))
            return new JsonObject();

        try
        {
            var node = JsonNode.Parse(setting.SettingsJson);
            return node as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
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

    private static HashSet<string> GetClrPropertyNames(Type type)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Prefer record constructor parameters when present (positional records).
        var ctor = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();
        if (ctor != null)
        {
            foreach (var p in ctor.GetParameters())
                names.Add(p.Name!);
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            names.Add(prop.Name);

        return names;
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

    public sealed record EventCatalogueRow(string EventTypeName, string TopicName, string ClrTypeName);
}
