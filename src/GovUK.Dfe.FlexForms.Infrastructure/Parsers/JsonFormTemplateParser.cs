using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Infrastructure.Parsers;

public class JsonFormTemplateParser : IFormTemplateParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [ExcludeFromCodeCoverage]
    public async Task<FormTemplate> ParseAsync(Stream templateStream, CancellationToken cancellationToken = default)
    {
        var template = await JsonSerializer.DeserializeAsync<FormTemplate>(templateStream, JsonOptions, cancellationToken);
        return template ?? throw new InvalidOperationException("Template could not be parsed.");
    }
}