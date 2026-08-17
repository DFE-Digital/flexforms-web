using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Domain.Tests.FormEngine;

public class FormStepPolicyTests
{
    [Theory]
    [InlineData(null, null, true, false, false, false)]
    [InlineData("", "", true, false, false, false)]
    [InlineData("task-1", null, false, true, false, false)]
    [InlineData("task-1", "", false, true, false, false)]
    [InlineData("task-1", "page-1", false, false, true, false)]
    [InlineData("task-1", "flow/f1/i1/p1", false, false, false, true)]
    [InlineData("task-1", "flow/f1", false, false, false, true)]
    [InlineData("task-1", "df1/derived/item1", false, false, true, false)]
    public void Step_flags_match_task_and_page_id(
        string? taskId,
        string? pageId,
        bool taskList,
        bool taskSummary,
        bool formPage,
        bool collectionFlowPage)
    {
        Assert.Equal(taskList, FormStepPolicy.IsTaskList(taskId, pageId));
        Assert.Equal(taskSummary, FormStepPolicy.IsTaskSummary(taskId, pageId));
        Assert.Equal(formPage, FormStepPolicy.IsFormPage(pageId));
        Assert.Equal(collectionFlowPage, FormStepPolicy.IsCollectionFlowPage(pageId));
        Assert.False(FormStepPolicy.IsApplicationPreview(pageId));
    }

    [Fact]
    public void Summary_mode_flags_follow_task_configuration()
    {
        var collection = TaskWithMode(FormStepPolicy.MultiCollectionFlowMode);
        var derived = TaskWithMode(FormStepPolicy.DerivedCollectionFlowMode);
        var standard = TaskWithMode("standard");

        Assert.True(FormStepPolicy.IsCollectionFlowSummary(collection));
        Assert.False(FormStepPolicy.IsDerivedCollectionFlowSummary(collection));
        Assert.True(FormStepPolicy.IsDerivedCollectionFlowSummary(derived));
        Assert.False(FormStepPolicy.IsCollectionFlowSummary(standard));
        Assert.False(FormStepPolicy.IsCollectionFlowSummary(null));
    }

    [Fact]
    public void IsInSubFlow_matches_flow_id_prefix()
    {
        Assert.True(FormStepPolicy.IsInSubFlow("f1", "flow/f1/i1/p1"));
        Assert.False(FormStepPolicy.IsInSubFlow("f1", "flow/f2/i1/p1"));
        Assert.False(FormStepPolicy.IsInSubFlow("f1", "page-1"));
    }

    [Fact]
    public void ResolvePage_returns_first_page_when_id_is_missing()
    {
        var pages = new[] { Page("p1"), Page("p2") };

        Assert.Equal("p1", FormStepPolicy.ResolvePage(pages, null)?.PageId);
        Assert.Equal("p2", FormStepPolicy.ResolvePage(pages, "p2")?.PageId);
        Assert.Null(FormStepPolicy.ResolvePage(pages, "missing"));
        Assert.Null(FormStepPolicy.ResolvePage(null, "p1"));
    }

    [Fact]
    public void GetNextPage_and_IsLastPage_walk_the_list()
    {
        var pages = new[] { Page("p1"), Page("p2"), Page("p3") };

        Assert.Equal("p2", FormStepPolicy.GetNextPage(pages, "p1")?.PageId);
        Assert.Equal("p3", FormStepPolicy.GetNextPage(pages, "p2")?.PageId);
        Assert.Null(FormStepPolicy.GetNextPage(pages, "p3"));
        Assert.Null(FormStepPolicy.GetNextPage(pages, "missing"));

        Assert.False(FormStepPolicy.IsLastPage(pages, "p1"));
        Assert.True(FormStepPolicy.IsLastPage(pages, "p3"));
        Assert.True(FormStepPolicy.IsLastPage(pages, "missing"));
        Assert.Equal(1, FormStepPolicy.IndexOfPage(pages, "p2"));
    }

    [Fact]
    public void Collection_flow_lookups_use_flow_id()
    {
        var flow = new MultiCollectionFlowConfiguration
        {
            FlowId = "f1",
            FieldId = "collection",
            Pages = [Page("p1")]
        };
        var task = new TaskModel
        {
            TaskId = "t1",
            TaskName = "Task",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Summary = new TaskSummaryConfiguration { Flows = [flow] }
        };

        Assert.Equal("collection", FormStepPolicy.GetCollectionFlowFieldId(task, "f1"));
        Assert.Equal("p1", FormStepPolicy.GetCollectionFlowPages(task, "f1")?[0].PageId);
        Assert.Null(FormStepPolicy.GetCollectionFlow(task, "missing"));
    }

    private static TaskModel TaskWithMode(string mode) =>
        new()
        {
            TaskId = "t1",
            TaskName = "Task",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Summary = new TaskSummaryConfiguration { Mode = mode }
        };

    private static Page Page(string id) =>
        new()
        {
            PageId = id,
            Slug = id,
            Title = id,
            Description = id,
            PageOrder = 1,
            Fields = []
        };
}
