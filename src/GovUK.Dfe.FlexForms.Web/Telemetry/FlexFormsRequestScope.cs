namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Ambient FlexForms form/application identifiers for the current request.
/// </summary>
public interface IFlexFormsRequestScope
{
    string? TemplateId { get; set; }
    string? ApplicationId { get; set; }
    string? ApplicationReference { get; set; }

    IReadOnlyDictionary<string, object> ToScopeDictionary();
}

/// <inheritdoc />
public sealed class FlexFormsRequestScope : IFlexFormsRequestScope
{
    public string? TemplateId { get; set; }
    public string? ApplicationId { get; set; }
    public string? ApplicationReference { get; set; }

    public IReadOnlyDictionary<string, object> ToScopeDictionary()
    {
        var scope = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        AddIfPresent(scope, FlexFormsLogContextKeys.TemplateId, TemplateId);
        AddIfPresent(scope, FlexFormsLogContextKeys.ApplicationId, ApplicationId);
        AddIfPresent(scope, FlexFormsLogContextKeys.ApplicationReference, ApplicationReference);
        return scope;
    }

    private static void AddIfPresent(IDictionary<string, object> scope, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            scope[key] = value;
    }
}
