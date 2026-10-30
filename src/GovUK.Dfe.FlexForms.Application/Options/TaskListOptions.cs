namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// Tenant-wide default labels for the task-list review and view buttons.
/// A template can override each field. Bound from the TaskList section.
/// </summary>
public class TaskListOptions
{
    public string? ReviewButtonText { get; set; }
    public string? ViewButtonText { get; set; }
}
