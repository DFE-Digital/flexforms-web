using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

[ExcludeFromCodeCoverage]
public class Option
{
    [JsonPropertyName("value")]
    public required string Value { get; set; }

    [JsonPropertyName("label")]
    public required string Label { get; set; }
}