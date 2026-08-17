namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// User-facing copy for Event Mappings. Keep these strings identical to the previous PageModel.
/// </summary>
public static class EventMappingsMessages
{
    public const string TenantContextMissing = "Tenant context is not available for this request.";

    public const string SelectTrigger = "Select a trigger.";

    public const string SelectEventType = "Select an event type.";

    public const string EnterMappingId = "Enter the mapping ID to use.";

    public const string EventKindMustBeTypedOrSchema = "Event kind must be Typed or Schema.";

    public const string SaveTriggerFailed = "Could not save event trigger.";

    public const string DeleteTriggerUnidentified = "Could not identify the trigger binding to remove.";

    public const string DeleteTriggerFailed = "Could not remove event trigger.";

    public const string SelectTemplate = "Select a template.";

    public const string SelectTenantTemplate = "Select a template that belongs to this tenant.";

    public const string EnterMappingJson = "Enter mapping JSON.";

    public const string MappingParseFailed = "Mapping JSON could not be parsed.";

    public const string MappingIdRequired = "mappingId is required.";

    public const string EventTypeMustMatch = "eventType in JSON must match the selected event type.";

    public const string FieldMappingsRequired = "fieldMappings must contain at least one property.";

    public const string SaveMappingFailed = "Could not save event mapping.";

    public const string EnterSchemaEventType = "Enter a schema event type name.";

    public const string EnterSchemaDefinitionJson = "Enter schema definition JSON.";

    public const string SchemaMustBeObject = "Schema definition must be a JSON object.";

    public const string TopicNameRequired = "topicName is required.";

    public const string JsonSchemaRequired = "jsonSchema is required.";

    public const string SaveSchemaFailed = "Could not save schema event.";

    public static string SystemOnlyEventType(string eventType) =>
        $"{eventType} is published by the platform for every upload and cannot be configured here.";

    public static string InvalidJson(string message) => $"Invalid JSON: {message}";

    public static string SavedTrigger(string eventType, string trigger) =>
        $"Saved {eventType} on the {trigger} trigger.";

    public static string RemovedTrigger(string eventType, string trigger) =>
        $"Removed {eventType} from the {trigger} trigger.";

    public static string SavedMapping(string keysLabel, string eventType) =>
        $"Saved mapping for template key(s) [{keysLabel}] / {eventType}.";

    public static string SavedSchema(string schemaKey) =>
        $"Saved schema event '{schemaKey}'.";

    public static string TypedEventNameClash(string schemaKey) =>
        $"'{schemaKey}' is a platform typed event. Choose a different name for schema events.";

    public static string UnknownProperty(string property, string eventType) =>
        $"Property '{property}' is not on {eventType}.";
}
