using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

[ExcludeFromCodeCoverage]
public class TaskGroup
{
    [JsonPropertyName("groupId")]
    public required string GroupId { get; set; }

    [JsonPropertyName("groupName")]
    public required string GroupName { get; set; }

    [JsonPropertyName("groupOrder")]
    public required int GroupOrder { get; set; }

    [JsonPropertyName("groupStatus")]
    public required string GroupStatus { get; set; }

    [JsonPropertyName("tasks")]
    public required List<Task> Tasks { get; set; }
}