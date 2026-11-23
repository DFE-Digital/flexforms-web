using System.Text.Json;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Domain.Tests.FormEngine;

public class TaskCompletionPolicyTests
{
    private static TaskModel Task(string? completion) => new()
    {
        TaskId = "t1",
        TaskName = "Task",
        TaskOrder = 1,
        TaskStatusString = "NotStarted",
        TaskCompletion = completion
    };

    private static FormTemplate Template(string? completion) => new()
    {
        TemplateId = "tpl",
        TemplateName = "Template",
        Description = "d",
        TaskGroups = [],
        TaskCompletion = completion
    };

    [Fact]
    public void Defaults_to_manual_when_nothing_is_set()
    {
        Assert.Equal(TaskCompletionMode.Manual, TaskCompletionPolicy.Resolve(Task(null), Template(null)));
        Assert.Equal(TaskCompletionMode.Manual, TaskCompletionPolicy.Resolve(null, null));
    }

    [Theory]
    [InlineData("automatic")]
    [InlineData("Automatic")]
    [InlineData(" automatic ")]
    public void Template_setting_applies_when_task_has_none(string value)
    {
        Assert.True(TaskCompletionPolicy.IsAutomatic(Task(null), Template(value)));
    }

    [Fact]
    public void Task_setting_overrides_template_setting()
    {
        Assert.False(TaskCompletionPolicy.IsAutomatic(Task("manual"), Template("automatic")));
        Assert.True(TaskCompletionPolicy.IsAutomatic(Task("automatic"), Template("manual")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("auto")]
    [InlineData("sometimes")]
    public void Unknown_values_mean_manual(string value)
    {
        Assert.False(TaskCompletionPolicy.IsRecognised(value));
        Assert.Equal(TaskCompletionMode.Manual, TaskCompletionPolicy.Resolve(Task(value), Template(value)));
    }

    [Fact]
    public void Unknown_task_value_falls_back_to_template_value()
    {
        Assert.True(TaskCompletionPolicy.IsAutomatic(Task("nonsense"), Template("automatic")));
    }

    [Fact]
    public void Existing_template_json_without_the_setting_stays_manual()
    {
        const string json = """
            {"templateId":"a","templateName":"b","description":"c","taskGroups":[
              {"groupId":"g","groupName":"G","groupOrder":1,"groupStatus":"NotStarted","tasks":[
                {"taskId":"t","taskName":"T","taskOrder":1,"taskStatus":"NotStarted"}]}]}
            """;

        var template = JsonSerializer.Deserialize<FormTemplate>(json)!;

        Assert.Null(template.TaskCompletion);
        Assert.Null(template.TaskGroups[0].Tasks[0].TaskCompletion);
        Assert.False(TaskCompletionPolicy.IsAutomatic(template.TaskGroups[0].Tasks[0], template));
    }

    [Fact]
    public void Setting_round_trips_through_json()
    {
        var template = Template("automatic");

        var json = JsonSerializer.Serialize(template);
        var back = JsonSerializer.Deserialize<FormTemplate>(json)!;

        Assert.Contains("\"taskCompletion\":\"automatic\"", json);
        Assert.Equal("automatic", back.TaskCompletion);
    }
}
