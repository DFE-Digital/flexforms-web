using GovUK.Dfe.FlexForms.Application.Admin;
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
    IEventMappingsAdmin eventMappingsAdmin,
    ITenantRequestContext tenantRequestContext,
    ITenantConfigurationCache tenantConfigurationCache,
    ITenantIdResolver tenantIdResolver) : PageModel
{
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
            return StayWithTenantError(error);

        var state = CaptureWorkState();
        await eventMappingsAdmin.LoadAsync(state, cancellationToken);
        ApplyWorkState(state);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveTriggerAsync(CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(MappingJson));
        ModelState.Remove(nameof(SchemaDefinitionJson));
        ModelState.Remove(nameof(NewSchemaEventType));

        if (!TryResolveTenant(out var error))
            return StayWithTenantError(error);

        return await DispatchAsync(state => eventMappingsAdmin.SaveTriggerAsync(state, cancellationToken));
    }

    public async Task<IActionResult> OnPostDeleteTriggerAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();

        if (!TryResolveTenant(out var error))
            return StayWithTenantError(error);

        return await DispatchAsync(state => eventMappingsAdmin.DeleteTriggerAsync(state, cancellationToken));
    }

    public async Task<IActionResult> OnPostSaveMappingAsync(CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(SchemaDefinitionJson));
        ModelState.Remove(nameof(NewSchemaEventType));

        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            var state = CaptureWorkState();
            await eventMappingsAdmin.LoadAsync(state, cancellationToken);
            ApplyWorkState(state);
            return Page();
        }

        return await DispatchAsync(state => eventMappingsAdmin.SaveMappingAsync(state, cancellationToken));
    }

    public async Task<IActionResult> OnPostSaveSchemaAsync(CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(MappingJson));

        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            var state = CaptureWorkState();
            await eventMappingsAdmin.LoadAsync(state, cancellationToken);
            ApplyWorkState(state);
            return Page();
        }

        return await DispatchAsync(state => eventMappingsAdmin.SaveSchemaAsync(state, cancellationToken));
    }

    private async Task<IActionResult> DispatchAsync(Func<EventMappingsWorkState, Task<AdminPageOutcome>> execute)
    {
        var state = CaptureWorkState();
        var outcome = await execute(state);
        ApplyWorkState(state);
        return MapOutcome(outcome);
    }

    private IActionResult StayWithTenantError(string? error)
    {
        HasError = true;
        ErrorMessage = error;
        return Page();
    }

    private EventMappingsWorkState CaptureWorkState() =>
        new()
        {
            TenantId = TenantId,
            TenantName = TenantName,
            SelectedTemplateId = SelectedTemplateId,
            SelectedEventType = SelectedEventType,
            SelectedSchemaEventType = SelectedSchemaEventType,
            MappingJson = MappingJson,
            SchemaDefinitionJson = SchemaDefinitionJson,
            NewSchemaEventType = NewSchemaEventType,
            TriggerName = TriggerName,
            TriggerEventKind = TriggerEventKind,
            TriggerEventType = TriggerEventType,
            TriggerMappingId = TriggerMappingId
        };

    private void ApplyWorkState(EventMappingsWorkState state)
    {
        TenantId = state.TenantId;
        TenantName = state.TenantName;
        TemplateOptions = ToSelectList(state.TemplateOptions);
        EventTypeOptions = ToSelectList(state.EventTypeOptions);
        Catalogue = state.Catalogue;
        SchemaEvents = state.SchemaEvents;
        SavedTypedMappings = state.SavedTypedMappings;
        TriggerOptions = ToSelectList(state.TriggerOptions);
        TriggerEventTypeOptions = ToSelectList(state.TriggerEventTypeOptions);
        SavedTriggers = state.SavedTriggers;
        AllowedTemplateKeys = state.AllowedTemplateKeys;
        ClrPropertyHints = state.ClrPropertyHints;
        ValidationWarnings = state.ValidationWarnings;
        CatalogueSource = state.CatalogueSource;
        SelectedTemplateId = state.SelectedTemplateId;
        SelectedEventType = state.SelectedEventType;
        SelectedSchemaEventType = state.SelectedSchemaEventType;
        MappingJson = state.MappingJson;
        SchemaDefinitionJson = state.SchemaDefinitionJson;
        NewSchemaEventType = state.NewSchemaEventType;
        if (state.HasError)
        {
            HasError = true;
            ErrorMessage = state.ErrorMessage;
        }
    }

    private IActionResult MapOutcome(AdminPageOutcome outcome)
    {
        foreach (var key in outcome.ModelStateKeysToRemove)
            ModelState.Remove(key);

        if (outcome.ClearModelState)
            ModelState.Clear();

        if (outcome.Errors.Count > 0)
        {
            foreach (var error in outcome.Errors)
                ModelState.AddModelError(error.FieldKey, error.Message);
        }

        if (outcome.RefreshLocalCaches)
        {
            tenantConfigurationCache.Invalidate(TenantId);
            tenantIdResolver.InvalidateHostnameCache();
        }

        if (outcome.SuccessMessage != null)
            TempData["EventMappingsSuccess"] = outcome.SuccessMessage;

        if (outcome.ErrorMessage != null && outcome.Kind == AdminPageOutcomeKind.RedirectToPage)
            TempData["EventMappingsError"] = outcome.ErrorMessage;
        else if (outcome.ErrorMessage != null)
        {
            HasError = true;
            ErrorMessage = outcome.ErrorMessage;
        }

        return outcome.Kind switch
        {
            AdminPageOutcomeKind.RedirectToPage => RedirectToPage(new
            {
                SelectedTemplateId = outcome.RouteValues.GetValueOrDefault("SelectedTemplateId"),
                SelectedEventType = outcome.RouteValues.GetValueOrDefault("SelectedEventType"),
                SelectedSchemaEventType = outcome.RouteValues.GetValueOrDefault("SelectedSchemaEventType")
            }),
            _ => Page()
        };
    }

    private static IReadOnlyList<SelectListItem> ToSelectList(IReadOnlyList<AdminSelectOption> options) =>
        options.Select(o => new SelectListItem(o.Text, o.Value, o.Selected)).ToList();

    private bool TryResolveTenant(out string? error)
    {
        if (tenantRequestContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            error = EventMappingsMessages.TenantContextMissing;
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
}
