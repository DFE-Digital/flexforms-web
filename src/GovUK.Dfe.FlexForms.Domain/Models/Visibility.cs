using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

[ExcludeFromCodeCoverage]
public class Visibility
{
    [JsonPropertyName("default")]
    public bool Default { get; set; }
}