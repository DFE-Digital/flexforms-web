using GovUK.Dfe.FlexForms.Domain.Models;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Domain.FormEngine;

public enum TaskCompletionMode
{
    Manual,
    Automatic
}

/// <summary>
/// Resolves how a task is marked complete: task setting, then template setting, then manual.
/// Missing or unrecognised values mean manual so existing templates are unchanged.
/// </summary>
public static class TaskCompletionPolicy
{
    public const string ManualValue = "manual";
    public const string AutomaticValue = "automatic";

    public static TaskCompletionMode Resolve(TaskModel? task, FormTemplate? template)
    {
        var taskValue = Parse(task?.TaskCompletion);
        if (taskValue.HasValue)
            return taskValue.Value;

        return Parse(template?.TaskCompletion) ?? TaskCompletionMode.Manual;
    }

    public static bool IsAutomatic(TaskModel? task, FormTemplate? template) =>
        Resolve(task, template) == TaskCompletionMode.Automatic;

    public static bool IsRecognised(string? value) => Parse(value).HasValue;

    private static TaskCompletionMode? Parse(string? value)
    {
        if (string.Equals(value?.Trim(), AutomaticValue, StringComparison.OrdinalIgnoreCase))
            return TaskCompletionMode.Automatic;
        if (string.Equals(value?.Trim(), ManualValue, StringComparison.OrdinalIgnoreCase))
            return TaskCompletionMode.Manual;
        return null;
    }
}
