using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

[ExcludeFromCodeCoverage]
public class Task
{
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }
        
    [JsonPropertyName("taskName")]
    public required string TaskName { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("taskOrder")]
    public required int TaskOrder { get; set; }

    [JsonPropertyName("taskStatus")]
    public required string TaskStatusString { get; set; }

    [JsonIgnore]
    public TaskStatus TaskStatus 
    { 
        get => Enum.TryParse<TaskStatus>(TaskStatusString, out var result) ? result : TaskStatus.NotStarted;
        set => TaskStatusString = value.ToString();
    }

    [JsonPropertyName("pages")]
    public List<Page>? Pages { get; set; }

    // When true, clicking a NotStarted task from the task list routes to its first page (instead of task summary).
    // Default is false/null for backward compatibility.
    [JsonPropertyName("startAtFirstPageWhenNotStarted")]
    public bool? StartAtFirstPageWhenNotStarted { get; set; }

    // Custom summary configuration (optional)
    [JsonPropertyName("summary")] public TaskSummaryConfiguration? Summary { get; set; }

    // Control visibility in main task list
    [JsonPropertyName("visibleInTaskList")] public bool? VisibleInTaskList { get; set; }
}

public class TaskSummaryConfiguration
{
    // "standard", "multiCollectionFlow", or "derivedCollectionFlow"
    [JsonPropertyName("mode")] public string Mode { get; set; } = "standard";

    // Custom page title and description for derived collection flows
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }

    // For multi-collection flow mode
    [JsonPropertyName("flows")] public List<MultiCollectionFlowConfiguration>? Flows { get; set; }
    
    // For derived collection flow mode
    [JsonPropertyName("derivedFlows")] public List<DerivedCollectionFlowConfiguration>? DerivedFlows { get; set; }
}

/// <summary>
/// Represents a column in the collection flow summary display
/// </summary>
public class FlowSummaryColumn
{
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("field")] public string Field { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for a single flow within a multi-collection flow task
/// </summary>
public class MultiCollectionFlowConfiguration
{
    [JsonPropertyName("flowId")] public string FlowId { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("fieldId")] public string FieldId { get; set; } = string.Empty;
    [JsonPropertyName("addButtonLabel")] public string AddButtonLabel { get; set; } = "Add item";
    [JsonPropertyName("minItems")] public int? MinItems { get; set; }
    [JsonPropertyName("maxItems")] public int? MaxItems { get; set; }
    [JsonPropertyName("itemKind")] public string? ItemKind { get; set; }
    [JsonPropertyName("itemKindPlural")] public string? ItemKindPlural { get; set; }
    [JsonPropertyName("itemTitleBinding")] public string? ItemTitleBinding { get; set; }
    [JsonPropertyName("summaryColumns")] public List<FlowSummaryColumn>? SummaryColumns { get; set; }
    [JsonPropertyName("addItemMessage")] public string? AddItemMessage { get; set; }
    [JsonPropertyName("updateItemMessage")] public string? UpdateItemMessage { get; set; }
    [JsonPropertyName("deleteItemMessage")] public string? DeleteItemMessage { get; set; }
    [JsonPropertyName("tableType")] public string TableType { get; set; } = "card"; // "card" or "list"
    [JsonPropertyName("pages")] public List<Page> Pages { get; set; } = new();
}

/// <summary>
/// Configuration for derived collection flows that generate forms based on other field values
/// </summary>
public class DerivedCollectionFlowConfiguration
{
    [JsonPropertyName("flowId")] public string FlowId { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("sourceFieldId")] public string SourceFieldId { get; set; } = string.Empty;
    [JsonPropertyName("sourceType")] public string SourceType { get; set; } = "autocomplete";
    [JsonPropertyName("fieldId")] public string FieldId { get; set; } = string.Empty;
    [JsonPropertyName("itemTitleBinding")] public string ItemTitleBinding { get; set; } = "name";
    [JsonPropertyName("sectionOrder")] public int SectionOrder { get; set; } = 1;
    [JsonPropertyName("signedMessage")] public string? SignedMessage { get; set; }
    [JsonPropertyName("statusField")] public string StatusField { get; set; } = "status";
    [JsonPropertyName("emptyStateMessage")] public string? EmptyStateMessage { get; set; }
    [JsonPropertyName("noItemsErrorMessage")] public string? NoItemsErrorMessage { get; set; }
    [JsonPropertyName("unsignedItemErrorMessage")] public string? UnsignedItemErrorMessage { get; set; }
    [JsonPropertyName("pages")] public List<Page> Pages { get; set; } = new();
}

/// <summary>
/// Represents a single item in a derived collection flow
/// </summary>
public class DerivedCollectionItem
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "Not signed yet";
    public Dictionary<string, object> PrefilledData { get; set; } = new();
    public Dictionary<string, object>? SourceData { get; set; }
}