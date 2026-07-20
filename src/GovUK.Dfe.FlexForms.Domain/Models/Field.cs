using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

[ExcludeFromCodeCoverage]
public class Field
{
    [JsonPropertyName("fieldId")]
    public required string FieldId { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("label")]
    public required Label Label { get; set; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; set; }

    [JsonPropertyName("tooltip")]
    public string? Tooltip { get; set; }

    [JsonPropertyName("required")]
    public bool? Required { get; set; }

    [JsonPropertyName("order")]
    public required int Order { get; set; }

    [JsonPropertyName("visibility")]
    public Visibility? Visibility { get; set; }
    [JsonPropertyName("validations")]
    public List<ValidationRule>? Validations { get; set; }

    [JsonPropertyName("options")]
    public List<Option>? Options { get; set; }

    [JsonPropertyName("complexField")]
    public ComplexField? ComplexField { get; set; }

    public string? Value { get; set; }
}