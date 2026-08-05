using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

/// <summary>
/// Optional dashboard layout for the applications list, stored on the template JSON.
/// Headings always come from the latest published template (Policy 1); cell values
/// are resolved from each application's own response by stable <see cref="DashboardColumnDefinition.FieldId"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class DashboardConfiguration
{
    /// <summary>
    /// Ordered column list. When omitted, the dashboard uses the default system columns only.
    /// At most <see cref="MaxCustomFieldColumns"/> entries with <c>type: "field"</c> are used.
    /// </summary>
    [JsonPropertyName("columns")]
    public List<DashboardColumnDefinition>? Columns { get; set; }

    public const int MaxCustomFieldColumns = 3;
}

/// <summary>
/// A single dashboard column. Use <c>type: "system"</c> for built-in columns
/// (<c>reference</c>, <c>dateStarted</c>, <c>dateSubmitted</c>, <c>status</c>, <c>action</c>)
/// or <c>type: "field"</c> with a stable form <see cref="FieldId"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class DashboardColumnDefinition
{
    /// <summary>
    /// <c>system</c> or <c>field</c>. Defaults to <c>field</c> when <see cref="FieldId"/> is set.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// System column id when <see cref="Type"/> is <c>system</c>.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Form answer key when <see cref="Type"/> is <c>field</c>. Must stay stable across template versions.
    /// </summary>
    [JsonPropertyName("fieldId")]
    public string? FieldId { get; set; }

    /// <summary>
    /// Column heading for field columns (and optional override for system columns).
    /// </summary>
    [JsonPropertyName("header")]
    public string? Header { get; set; }

    /// <summary>
    /// Sort order. Lower first. When omitted, array order is used.
    /// </summary>
    [JsonPropertyName("order")]
    public int? Order { get; set; }
}
