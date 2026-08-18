using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class FormStateManagerTests
{
    private readonly FormStateManager _manager = new();

    [Theory]
    [InlineData(null, null, FormState.TaskList)]
    [InlineData("task-1", null, FormState.TaskSummary)]
    [InlineData("task-1", "page-1", FormState.FormPage)]
    [InlineData("task-1", "flow/f1/i1/p1", FormState.SubFlowPage)]
    public void GetCurrentState_maps_route_tokens_to_form_state(string? taskId, string? pageId, FormState expected)
    {
        Assert.Equal(expected, _manager.GetCurrentState("REF-1", taskId ?? string.Empty, pageId ?? string.Empty));
    }

    [Fact]
    public void Summary_flags_delegate_to_step_policy()
    {
        var collection = new TaskModel
        {
            TaskId = "t1",
            TaskName = "Task",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Summary = new TaskSummaryConfiguration { Mode = FormStepPolicy.MultiCollectionFlowMode }
        };
        var derived = new TaskModel
        {
            TaskId = "t2",
            TaskName = "Task",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Summary = new TaskSummaryConfiguration { Mode = FormStepPolicy.DerivedCollectionFlowMode }
        };

        Assert.True(_manager.ShouldShowCollectionFlowSummary(collection));
        Assert.True(_manager.ShouldShowDerivedCollectionFlowSummary(derived));
        Assert.False(_manager.ShouldShowApplicationPreview("preview"));
        Assert.True(_manager.ShouldShowTaskList(string.Empty));
        Assert.True(_manager.IsInSubFlow("f1", "flow/f1/i1"));
    }
}
