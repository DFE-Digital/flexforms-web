using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;
using PageModel = GovUK.Dfe.FlexForms.Domain.Models.Page;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class PrepareFormEngineGetServiceCoverageTests
{
    private readonly ITemplateManagementService _templates = Substitute.For<ITemplateManagementService>();
    private readonly IApplicationResponseService _responses = Substitute.For<IApplicationResponseService>();
    private readonly ICollectionFlowProgressStore _flowProgress = Substitute.For<ICollectionFlowProgressStore>();
    private readonly IFormSessionStore _session = Substitute.For<IFormSessionStore>();
    private readonly IConditionalLogicOrchestrator _conditionalLogic = Substitute.For<IConditionalLogicOrchestrator>();
    private readonly IFormStateManager _formState = Substitute.For<IFormStateManager>();
    private readonly IApplicationsClient _applications = Substitute.For<IApplicationsClient>();
    private readonly INavigationHistoryService _history = Substitute.For<INavigationHistoryService>();
    private readonly IApplicationStateService _applicationState = Substitute.For<IApplicationStateService>();
    private readonly PrepareFormEngineGetService _service;

    public PrepareFormEngineGetServiceCoverageTests()
    {
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object>());
        _flowProgress.Load(Arg.Any<string>(), Arg.Any<string>()).Returns(new Dictionary<string, object>());
        _conditionalLogic.ApplyConditionalLogicAsync(default!, default!, default)
            .ReturnsForAnyArgs(new FormConditionalState());
        _applications.GetFilesForApplicationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _applications.GetFileValidationGateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new FileValidationGateDto { CanSubmit = true, BlockingFiles = [] });
        _formState.ShouldShowCollectionFlowSummary(Arg.Any<TaskModel>()).Returns(false);
        _formState.ShouldShowDerivedCollectionFlowSummary(Arg.Any<TaskModel>()).Returns(false);
        _applicationState.CalculateTaskStatus(default!, default!, default!, default, default!)
            .ReturnsForAnyArgs(Domain.Models.TaskStatus.InProgress);

        _service = new PrepareFormEngineGetService(
            _templates,
            _responses,
            _flowProgress,
            _session,
            _conditionalLogic,
            _formState,
            Substitute.For<IFormFileFieldService>(),
            Substitute.For<IComplexFieldConfigurationService>(),
            Substitute.For<IDerivedCollectionFlowService>(),
            _applications,
            _history,
            _applicationState,
            NullLogger<PrepareFormEngineGetService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStayWithTaskListState_WhenNoTaskOrPage()
    {
        var state = BaseState();
        var result = await _service.ExecuteAsync(state, isPreview: false, isBackNav: false, isEditable: true);

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(FormState.TaskList, result.FormState);
        Assert.Null(state.CurrentTask);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetTaskSummaryState_WhenMultiCollectionTaskWithoutPage()
    {
        var task = CreateCollectionFlowTask("f1", "members", CreatePage("fp1"));
        Register(task);
        _formState.ShouldShowCollectionFlowSummary(task).Returns(true);

        var state = BaseState(taskId: task.TaskId);
        var result = await _service.ExecuteAsync(state, isPreview: false, isBackNav: false, isEditable: true);

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(FormState.TaskSummary, state.CurrentFormState);
        Assert.Equal(FormState.TaskSummary, result.FormState);
        Assert.Equal(task.TaskId, state.CurrentTask!.TaskId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetDerivedSummaryState_WhenDerivedTaskWithoutPage()
    {
        var task = CreateDerivedFlowTask("df1", "sources");
        Register(task);
        _formState.ShouldShowDerivedCollectionFlowSummary(task).Returns(true);

        var state = BaseState(taskId: task.TaskId);
        var result = await _service.ExecuteAsync(state, isPreview: false, isBackNav: false, isEditable: true);

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(FormState.DerivedCollectionFlowSummary, state.CurrentFormState);
        Assert.Equal(FormState.DerivedCollectionFlowSummary, result.FormState);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldResolveCollectionFlowPage_AndMergeProgress()
    {
        var fp1 = CreatePage("fp1");
        var task = CreateCollectionFlowTask("f1", "members", fp1);
        Register(task, fp1);
        _flowProgress.Load("f1", "i1").Returns(new Dictionary<string, object> { ["name"] = "Ada" });
        _session.GetString(Arg.Any<string>()).Returns((string?)null);

        var state = BaseState(taskId: task.TaskId, pageId: "flow/f1/i1/fp1");
        var result = await _service.ExecuteAsync(state, isPreview: false, isBackNav: false, isEditable: true);

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(FormState.FormPage, state.CurrentFormState);
        Assert.Equal("fp1", state.CurrentPage!.PageId);
        Assert.Equal("Ada", state.Data["name"].ToString());
        Assert.Equal("f1", state.FlowId);
        Assert.Equal("i1", state.InstanceId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldResolveStandardFormPage_WhenPageIdPresent()
    {
        var page = CreatePage("p1");
        var task = CreateStandardTask(page);
        Register(task, page);

        var state = BaseState(taskId: task.TaskId, pageId: "p1");
        var result = await _service.ExecuteAsync(state, isPreview: false, isBackNav: false, isEditable: true);

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal("p1", state.CurrentPage!.PageId);
        Assert.Equal(task.TaskId, state.CurrentTask!.TaskId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldBlockSubmitOnPreview_WhenFileValidationGateFails()
    {
        var applicationId = Guid.NewGuid();
        _applications.GetFileValidationGateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(new FileValidationGateDto
            {
                CanSubmit = false,
                BlockingFiles = [new FileValidationBlockDto { OriginalFileName = "scan.pdf" }]
            });

        var state = BaseState(applicationId: applicationId);
        var result = await _service.ExecuteAsync(state, isPreview: true, isBackNav: false, isEditable: true);

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(FormState.ApplicationPreview, state.CurrentFormState);
        Assert.True(result.FileValidationBlocksSubmit);
        Assert.Single(result.BlockingFiles);
        Assert.Equal("scan.pdf", result.BlockingFiles[0].OriginalFileName);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPopNavigationHistory_WhenBackNav()
    {
        var page = CreatePage("p1");
        var task = CreateStandardTask(page);
        Register(task, page);

        var state = BaseState(taskId: task.TaskId, pageId: "p1");
        await _service.ExecuteAsync(state, isPreview: false, isBackNav: true, isEditable: true);

        _history.Received().Pop($"REF-1:{task.TaskId}");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPopFlowScopedHistory_WhenBackNavOnCollectionFlow()
    {
        var fp1 = CreatePage("fp1");
        var task = CreateCollectionFlowTask("f1", "members", fp1);
        Register(task, fp1);
        _session.GetString(Arg.Any<string>()).Returns((string?)null);

        var state = BaseState(taskId: task.TaskId, pageId: "flow/f1/i1/fp1");
        await _service.ExecuteAsync(state, isPreview: false, isBackNav: true, isEditable: true);

        _history.Received().Pop($"REF-1:{task.TaskId}:flow:f1:i1");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMarkTaskCompletedOnSummary_WhenTaskStatusIsCompleted()
    {
        var task = CreateStandardTask(CreatePage("p1"));
        Register(task);
        _applicationState.CalculateTaskStatus(task.TaskId, Arg.Any<FormTemplate>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<Guid?>(), Arg.Any<string>())
            .Returns(Domain.Models.TaskStatus.Completed);

        var state = BaseState(taskId: task.TaskId);
        state.CurrentFormState = FormState.TaskSummary;
        var result = await _service.ExecuteAsync(state, isPreview: false, isBackNav: false, isEditable: true);

        Assert.True(result.IsTaskCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMergeFlowProgressIntoFormData_OnMultiCollectionSummary()
    {
        var task = CreateCollectionFlowTask("f1", "members", CreatePage("fp1"));
        Register(task);
        _formState.ShouldShowCollectionFlowSummary(task).Returns(true);
        _flowProgress.Load("f1", "i1").Returns(new Dictionary<string, object> { ["role"] = "Lead" });

        var state = BaseState(taskId: task.TaskId);
        state.FormData["members"] = """[{"id":"i1","name":"Ada"}]""";
        state.Data["members"] = state.FormData["members"];

        await _service.ExecuteAsync(state, isPreview: false, isBackNav: false, isEditable: true);

        Assert.Contains("Lead", state.FormData["members"].ToString());
        Assert.Contains("Lead", state.Data["members"].ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldClearAccumulatedData_WhenApplicationIdChanges()
    {
        var previousId = Guid.NewGuid().ToString();
        _session.GetString(FormSessionKeys.CurrentAccumulatedApplicationId).Returns(previousId);

        var state = BaseState(applicationId: Guid.NewGuid());
        await _service.ExecuteAsync(state, isPreview: false, isBackNav: false, isEditable: true);

        _responses.Received().ClearAccumulatedFormData();
    }

    private void Register(TaskModel task, PageModel? page = null)
    {
        var group = new TaskGroup { GroupId = "g1", GroupName = "g", GroupOrder = 1, GroupStatus = "NotStarted", Tasks = [task] };
        _templates.FindTask(Arg.Any<FormTemplate>(), Arg.Any<string>()).Returns((group, task));
        if (page != null)
            _templates.FindPage(Arg.Any<FormTemplate>(), Arg.Any<string>()).Returns((group, task, page));
    }

    private static FormEngineWorkState BaseState(string taskId = "", string pageId = "", Guid? applicationId = null) =>
        new()
        {
            ReferenceNumber = "REF-1",
            TaskId = taskId,
            CurrentPageId = pageId,
            ApplicationId = applicationId ?? Guid.NewGuid(),
            Template = new FormTemplate { TemplateId = "tpl", TemplateName = "tpl", Description = "tpl", TaskGroups = [] },
            FormData = new Dictionary<string, object>(),
            Data = new Dictionary<string, object>()
        };

    private static PageModel CreatePage(string pageId) =>
        new()
        {
            PageId = pageId,
            Slug = pageId,
            Title = pageId,
            Description = pageId,
            PageOrder = 1,
            Fields = []
        };

    private static TaskModel CreateStandardTask(params PageModel[] pages) =>
        new()
        {
            TaskId = "t1",
            TaskName = "About you",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [.. pages]
        };

    private static TaskModel CreateCollectionFlowTask(string flowId, string fieldId, params PageModel[] flowPages) =>
        new()
        {
            TaskId = "t1",
            TaskName = "Members",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [],
            Summary = new TaskSummaryConfiguration
            {
                Mode = FormStepPolicy.MultiCollectionFlowMode,
                Flows =
                [
                    new MultiCollectionFlowConfiguration
                    {
                        FlowId = flowId,
                        FieldId = fieldId,
                        Title = "Members",
                        Pages = [.. flowPages]
                    }
                ]
            }
        };

    private static TaskModel CreateDerivedFlowTask(string flowId, string sourceFieldId) =>
        new()
        {
            TaskId = "t1",
            TaskName = "Declarations",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [],
            Summary = new TaskSummaryConfiguration
            {
                Mode = FormStepPolicy.DerivedCollectionFlowMode,
                DerivedFlows =
                [
                    new DerivedCollectionFlowConfiguration
                    {
                        FlowId = flowId,
                        SourceFieldId = sourceFieldId,
                        FieldId = "declarations",
                        Title = "Declarations"
                    }
                ]
            }
        };
}
