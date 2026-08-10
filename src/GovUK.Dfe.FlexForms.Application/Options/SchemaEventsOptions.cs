namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// Tenant-defined schema events (Phase 3). Bound from SchemaEvents TenantConfig category.
/// Keys are message type names used in ApplicationSubmission and EventMappings.
/// </summary>
public class SchemaEventsOptions : Dictionary<string, SchemaEventDefinitionOptions>
{
}

/// <summary>
/// Definition of one tenant schema event.
/// </summary>
public class SchemaEventDefinitionOptions
{
    /// <summary>Azure Service Bus topic name to publish to.</summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>Optional semantic version for the payload contract.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Human-readable description.</summary>
    public string? Description { get; set; }

    /// <summary>JSON Schema object describing the payload (stored as nested config).</summary>
    public Dictionary<string, object?>? JsonSchema { get; set; }
}
