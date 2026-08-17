using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Mutable view-state bag shared between the PageModel and form-engine use cases.
/// </summary>
public sealed class FormEngineWorkState
{
    public string ReferenceNumber { get; set; } = string.Empty;

    public string TaskId { get; set; } = string.Empty;

    public string CurrentPageId { get; set; } = string.Empty;

    public Guid? ApplicationId { get; set; }

    public string ApplicationStatus { get; set; } = "InProgress";

    public FormTemplate? Template { get; set; }

    public Dictionary<string, object> FormData { get; set; } = new();

    public Dictionary<string, object> Data { get; set; } = new();

    public FormState CurrentFormState { get; set; }

    public TaskGroup? CurrentGroup { get; set; }

    public TaskModel? CurrentTask { get; set; }

    public Page? CurrentPage { get; set; }

    public string? FlowId { get; set; }

    public string? InstanceId { get; set; }

    public string? FlowPageId { get; set; }

    public string? DerivedFlowId { get; set; }

    public string? DerivedItemId { get; set; }

    public string? DerivedPageId { get; set; }

    public FormConditionalState? ConditionalState { get; set; }

    public bool IsEditable { get; set; }

    public bool IsTaskCompleted { get; set; }

    public string ErrorContextKey => $"{ReferenceNumber}_{TaskId}_{CurrentPageId}";

    public FormFileFieldContext FileFieldContext => new(ApplicationId, FlowId, InstanceId);
}
