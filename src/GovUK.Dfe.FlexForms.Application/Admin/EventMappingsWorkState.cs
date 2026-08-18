namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the Event Mappings admin page.
/// </summary>
public sealed class EventMappingsWorkState
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public bool HasError { get; set; }

    public string? ErrorMessage { get; set; }

    public IReadOnlyList<AdminSelectOption> TemplateOptions { get; set; } = [];

    public IReadOnlyList<AdminSelectOption> EventTypeOptions { get; set; } = [];

    public IReadOnlyList<EventCatalogueRow> Catalogue { get; set; } = [];

    public IReadOnlyList<SchemaEventRow> SchemaEvents { get; set; } = [];

    public IReadOnlyList<SavedMappingRow> SavedTypedMappings { get; set; } = [];

    public IReadOnlyList<AdminSelectOption> TriggerOptions { get; set; } = [];

    public IReadOnlyList<AdminSelectOption> TriggerEventTypeOptions { get; set; } = [];

    public IReadOnlyList<TriggerBindingRow> SavedTriggers { get; set; } = [];

    public IReadOnlySet<string> AllowedTemplateKeys { get; set; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> ClrPropertyHints { get; set; } = [];

    public IReadOnlyList<string> ValidationWarnings { get; set; } = [];

    public string? CatalogueSource { get; set; }

    public string? SelectedTemplateId { get; set; }

    public string? SelectedEventType { get; set; }

    public string? SelectedSchemaEventType { get; set; }

    public string? MappingJson { get; set; }

    public string? SchemaDefinitionJson { get; set; }

    public string? NewSchemaEventType { get; set; }

    public string? TriggerName { get; set; }

    public string? TriggerEventKind { get; set; }

    public string? TriggerEventType { get; set; }

    public string? TriggerMappingId { get; set; }
}
