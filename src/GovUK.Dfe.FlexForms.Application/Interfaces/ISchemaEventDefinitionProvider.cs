using GovUK.Dfe.FlexForms.Application.Options;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Resolves tenant-defined schema event definitions from SchemaEvents TenantConfig.
/// </summary>
public interface ISchemaEventDefinitionProvider
{
    /// <summary>
    /// Gets a schema event definition by message type name, or null if not configured.
    /// </summary>
    SchemaEventDefinitionOptions? GetDefinition(string messageType);

    /// <summary>
    /// Returns all configured schema event definitions for the current tenant.
    /// </summary>
    IReadOnlyDictionary<string, SchemaEventDefinitionOptions> GetAll();
}
