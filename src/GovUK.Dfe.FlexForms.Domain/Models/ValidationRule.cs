using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Domain.Models;

[ExcludeFromCodeCoverage]
public class ValidationRule
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }            // "required", "regex", "maxLength"

    [JsonPropertyName("rule")]
    public required object Rule { get; set; }            // pattern or numeric limit

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("condition")]
    public Condition? Condition { get; set; }    // optional conditional application
}