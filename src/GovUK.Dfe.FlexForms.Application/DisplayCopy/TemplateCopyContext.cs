using System.Text.Json;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.DisplayCopy;

/// <summary>
/// The template whose wording applies to the current request.
/// Set while loading a form or the dashboard, then read by the display-copy resolver.
/// </summary>
public interface ITemplateCopyContext
{
    FormTemplate? Template { get; }

    void Use(FormTemplate? template);

    bool TryUseJson(string? json);
}

public sealed class TemplateCopyContext : ITemplateCopyContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FormTemplate? Template { get; private set; }

    public void Use(FormTemplate? template) => Template = template;

    public bool TryUseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var template = JsonSerializer.Deserialize<FormTemplate>(json, JsonOptions);
            if (template is null)
                return false;

            Template = template;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
