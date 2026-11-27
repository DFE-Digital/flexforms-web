using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;
using TaskStatus = GovUK.Dfe.FlexForms.Domain.Models.TaskStatus;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class AutoCompleteTaskServiceTests
{
    private readonly IApplicationStateService _state = Substitute.For<IApplicationStateService>();
    private readonly IFieldRequirementService _requirements = Substitute.For<IFieldRequirementService>();
    private readonly IDerivedCollectionFlowService _derived = Substitute.For<IDerivedCollectionFlowService>();
    private readonly IConditionalLogicOrchestrator _conditional = Substitute.For<IConditionalLogicOrchestrator>();
    private readonly Guid _appId = Guid.NewGuid();

    private AutoCompleteTaskService Sut() =>
        new(_state, _requirements, _derived, _conditional, NullLogger<AutoCompleteTaskService>.Instance);

    private static TaskModel NewTask(string? completion) => new()
    {
        TaskId = "t1",
        TaskName = "Task",
        TaskOrder = 1,
        TaskStatusString = "NotStarted",
        TaskCompletion = completion,
        Pages =
        [
            new Page
            {
                PageId = "p1", Slug = "p1", Title = "P", Description = "d", PageOrder = 1,
                Fields = [new Field { FieldId = "f1", Type = "text", Label = new Label { Value = "F" }, Order = 1, Required = true }]
            }
        ]
    };

    private FormEngineWorkState NewState(TaskModel task, string? templateCompletion = null, bool editable = true, string appStatus = "InProgress", params TaskModel[] otherTasks) => new()
    {
        ApplicationId = _appId,
        ApplicationStatus = appStatus,
        IsEditable = editable,
        CurrentTask = task,
        TaskId = task.TaskId,
        Template = new FormTemplate
        {
            TemplateId = "tpl", TemplateName = "T", Description = "d",
            TaskGroups = [new TaskGroup { GroupId = "g1", GroupName = "G", GroupOrder = 1, GroupStatus = "NotStarted", Tasks = [task, .. otherTasks] }],
            TaskCompletion = templateCompletion
        }
    };

    private void Missing(params string[] messages)
    {
        var dict = messages.Select((m, i) => (m, i)).ToDictionary(x => $"f{x.i}", x => x.m);
        _requirements.GetMissingRequiredFieldsWithMessages(default!, default!, default!, default).ReturnsForAnyArgs(dict);
        _requirements.IsFieldRequired(default!, default!).ReturnsForAnyArgs(true);
    }

    private void CurrentStatus(TaskStatus status) =>
        _state.CalculateTaskStatus(default!, default!, default!, default, default!).ReturnsForAnyArgs(status);

    private void NoDerivedItems()
    {
        _derived.GenerateItemsFromSourceField(default!, default!, default!).ReturnsForAnyArgs(new List<DerivedCollectionItem>());
        _derived.GetItemStatuses(default!, default!).ReturnsForAnyArgs(new Dictionary<string, string>());
    }

    [Fact]
    public async Task Manual_task_is_never_touched()
    {
        Missing();
        var result = await Sut().EvaluateAsync(NewState(NewTask(null)), new());

        Assert.False(result.IsAutomatic);
        await _state.DidNotReceiveWithAnyArgs().SaveTaskStatusAsync(default, default!, default);
    }

    [Fact]
    public async Task Automatic_task_is_completed_when_nothing_is_missing()
    {
        Missing();
        CurrentStatus(TaskStatus.InProgress);

        var result = await Sut().EvaluateAsync(NewState(NewTask("automatic")), new());

        Assert.True(result.IsComplete);
        await _state.Received(1).SaveTaskStatusAsync(_appId, "t1", TaskStatus.Completed);
    }

    [Fact]
    public async Task Template_level_setting_enables_automatic_completion()
    {
        Missing();
        CurrentStatus(TaskStatus.InProgress);

        var result = await Sut().EvaluateAsync(NewState(NewTask(null), "automatic"), new());

        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task Already_completed_task_is_not_saved_again()
    {
        Missing();
        CurrentStatus(TaskStatus.Completed);

        await Sut().EvaluateAsync(NewState(NewTask("automatic")), new());

        await _state.DidNotReceiveWithAnyArgs().SaveTaskStatusAsync(default, default!, default);
    }

    [Fact]
    public async Task Incomplete_task_reports_outstanding_and_is_not_completed()
    {
        Missing("Enter F");
        CurrentStatus(TaskStatus.InProgress);

        var result = await Sut().EvaluateAsync(NewState(NewTask("automatic")), new());

        Assert.True(result.IsAutomatic);
        Assert.False(result.IsComplete);
        Assert.Contains("Enter F", result.Outstanding);
        await _state.DidNotReceiveWithAnyArgs().SaveTaskStatusAsync(default, default!, default);
    }

    [Fact]
    public async Task Completed_task_reverts_to_in_progress_when_it_becomes_incomplete()
    {
        Missing("Enter F");
        CurrentStatus(TaskStatus.Completed);

        await Sut().EvaluateAsync(NewState(NewTask("automatic")), new() { ["other"] = "x" });

        await _state.Received(1).SaveTaskStatusAsync(_appId, "t1", Arg.Is<TaskStatus>(s => s != TaskStatus.Completed));
    }

    [Fact]
    public async Task Read_only_and_submitted_contexts_are_skipped()
    {
        Missing();
        CurrentStatus(TaskStatus.InProgress);

        var readOnly = await Sut().EvaluateAsync(NewState(NewTask("automatic"), editable: false), new());
        var submitted = await Sut().EvaluateAsync(NewState(NewTask("automatic"), appStatus: "Submitted"), new());

        Assert.False(readOnly.IsAutomatic);
        Assert.False(submitted.IsAutomatic);
        await _state.DidNotReceiveWithAnyArgs().SaveTaskStatusAsync(default, default!, default);
    }

    [Fact]
    public async Task Task_without_completion_criteria_stays_manual()
    {
        _requirements.IsFieldRequired(default!, default!).ReturnsForAnyArgs(false);

        var result = await Sut().EvaluateAsync(NewState(NewTask("automatic")), new());

        Assert.False(result.IsAutomatic);
        await _state.DidNotReceiveWithAnyArgs().SaveTaskStatusAsync(default, default!, default);
    }

    [Fact]
    public async Task Exceptions_are_swallowed()
    {
        _requirements.IsFieldRequired(default!, default!).ThrowsForAnyArgs(new InvalidOperationException());

        var result = await Sut().EvaluateAsync(NewState(NewTask("automatic")), new());

        Assert.False(result.IsAutomatic);
    }

    [Fact]
    public async Task Dependent_derived_task_is_reevaluated_when_source_field_belongs_to_current_task()
    {
        Missing();
        NoDerivedItems();
        CurrentStatus(TaskStatus.InProgress);
        var source = NewTask(null);
        var dependent = DependentTask("d1", sourceFieldId: "f1");

        await Sut().EvaluateAsync(NewState(source, otherTasks: dependent), new());

        _state.Received(1).CalculateTaskStatus("d1", Arg.Any<FormTemplate>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<Guid?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Dependent_derived_task_is_reevaluated_when_source_field_is_a_collection_flow()
    {
        Missing();
        NoDerivedItems();
        CurrentStatus(TaskStatus.InProgress);
        var source = NewTask(null);
        source.Summary = new TaskSummaryConfiguration
        {
            Flows = [new MultiCollectionFlowConfiguration { FieldId = "flow1" }]
        };
        var dependent = DependentTask("d1", sourceFieldId: "flow1");

        await Sut().EvaluateAsync(NewState(source, otherTasks: dependent), new());

        _state.Received(1).CalculateTaskStatus("d1", Arg.Any<FormTemplate>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<Guid?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Unrelated_dependent_task_is_not_reevaluated()
    {
        Missing();
        NoDerivedItems();
        CurrentStatus(TaskStatus.InProgress);
        var dependent = DependentTask("d1", sourceFieldId: "other-field");

        await Sut().EvaluateAsync(NewState(NewTask("automatic"), otherTasks: dependent), new());

        await _state.DidNotReceive().SaveTaskStatusAsync(_appId, "d1", Arg.Any<TaskStatus>());
    }

    [Fact]
    public async Task Current_task_is_not_treated_as_its_own_dependent()
    {
        Missing();
        NoDerivedItems();
        CurrentStatus(TaskStatus.InProgress);
        var source = NewTask("automatic");
        source.Summary = new TaskSummaryConfiguration
        {
            DerivedFlows = [new DerivedCollectionFlowConfiguration { SourceFieldId = "f1" }]
        };

        await Sut().EvaluateAsync(NewState(source), new());

        _state.Received(1).CalculateTaskStatus("t1", Arg.Any<FormTemplate>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<Guid?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Manual_current_task_still_reevaluates_dependent_derived_tasks()
    {
        Missing();
        NoDerivedItems();
        CurrentStatus(TaskStatus.InProgress);
        var dependent = DependentTask("d1", sourceFieldId: "f1");

        await Sut().EvaluateAsync(NewState(NewTask(null), otherTasks: dependent), new());

        _state.Received(1).CalculateTaskStatus("d1", Arg.Any<FormTemplate>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<Guid?>(), Arg.Any<string>());
    }

    private static TaskModel DependentTask(string taskId, string sourceFieldId) => new()
    {
        TaskId = taskId,
        TaskName = "Dependent",
        TaskOrder = 2,
        TaskStatusString = "NotStarted",
        TaskCompletion = "automatic",
        Pages = [],
        Summary = new TaskSummaryConfiguration
        {
            DerivedFlows = [new DerivedCollectionFlowConfiguration { SourceFieldId = sourceFieldId }]
        }
    };
}
