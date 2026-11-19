using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

/// <summary>
/// Per-field overrides for the file validation status labels shown in the "Uploaded files" table.
/// Blank or missing values fall back to the built-in defaults.
/// </summary>
public class FileValidationStatusText
{
    [JsonPropertyName("pending")]
    public string? Pending { get; set; }

    [JsonPropertyName("passed")]
    public string? Passed { get; set; }

    [JsonPropertyName("failed")]
    public string? Failed { get; set; }
}
