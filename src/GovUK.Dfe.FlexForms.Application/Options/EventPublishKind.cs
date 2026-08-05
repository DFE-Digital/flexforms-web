namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// Whether a publish entry targets a CoreLibs typed event or a tenant-defined schema event.
/// </summary>
public static class EventPublishKind
{
    public const string Typed = "Typed";
    public const string Schema = "Schema";
}
