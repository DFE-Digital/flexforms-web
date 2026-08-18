using System.Collections.ObjectModel;
using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;
using PageModel = GovUK.Dfe.FlexForms.Domain.Models.Page;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class CompleteFormTaskServiceTests
{
    private readonly IApplicationStateService _applicationState = Substitute.For<IApplicationStateService>();
    private readonly IFieldRequirementService _fieldRequirements = Substitute.For<IFieldRequirementService>();
    private readonly IConditionalLogicOrchestrator _conditionalLogic = Substitute.For<IConditionalLogicOrchestrator>();
    private readonly CompleteFormTaskService _service;

    public CompleteFormTaskServiceTests()
    {
        _conditionalLogic.ApplyConditionalLogicAsync(default!, default!, default)
            .ReturnsForAnyArgs(new FormConditionalState());
        _fieldRequirements.GetMissingRequiredFieldsWithMessages(default!, default!, default!, default)
            .ReturnsForAnyArgs(new Dictionary<string, string>());
        _fieldRequirements.IsFieldRequired(default!, default!).ReturnsForAnyArgs(false);
        _service = new CompleteFormTaskService(
            _applicationState,
            _fieldRequirements,
            _conditionalLogic,
            NullLogger<CompleteFormTaskService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStayOnPage_WhenRequiredFieldsAreMissing()
    {
        var task = CreateTask("t1");
        _fieldRequirements.GetMissingRequiredFieldsWithMessages(default!, default!, default!, default)
            .ReturnsForAnyArgs(new Dictionary<string, string> { ["name"] = "Enter a name" });

        var result = await _service.ExecuteAsync(State(task, isTaskCompleted: true));

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(FormState.TaskSummary, result.FormState);
        Assert.False(result.IsTaskCompleted);
        Assert.Contains(result.Errors, e => e.Message.Contains("Enter a name"));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStayOnPage_WhenCollectionMinItemsAreMissing()
    {
        var flow = new MultiCollectionFlowConfiguration
        {
            FlowId = "f1",
            FieldId = "members",
            Title = "Members",
            MinItems = 2,
            Pages = []
        };
        var task = CreateTask("t1", FormStepPolicy.MultiCollectionFlowMode, [flow]);
        var state = State(task, isTaskCompleted: true);
        state.FormData["members"] = """[{"id":"i1"}]""";

        var result = await _service.ExecuteAsync(state);

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message.Contains("Add at least 2 item(s) to Members"));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectToTaskList_WhenTaskCanBeCompleted()
    {
        var task = CreateTask("t1");
        var state = State(task, isTaskCompleted: true);

        var result = await _service.ExecuteAsync(state);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal("/applications/REF-1", result.RedirectUrl);
        await _applicationState.Received().SaveTaskStatusAsync(state.ApplicationId!.Value, "t1", Domain.Models.TaskStatus.Completed);
    }

    private static FormEngineWorkState State(TaskModel task, bool isTaskCompleted) =>
        new()
        {
            ReferenceNumber = "REF-1",
            TaskId = task.TaskId,
            ApplicationId = Guid.NewGuid(),
            ApplicationStatus = "InProgress",
            Template = new FormTemplate
            {
                TemplateId = "tpl",
                TemplateName = "tpl",
                Description = "tpl",
                TaskGroups = []
            },
            FormData = new Dictionary<string, object>(),
            CurrentTask = task,
            IsTaskCompleted = isTaskCompleted
        };

    private static TaskModel CreateTask(
        string id,
        string mode = "standard",
        List<MultiCollectionFlowConfiguration>? flows = null) =>
        new()
        {
            TaskId = id,
            TaskName = "About you",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [new Page { PageId = "p1", Slug = "p1", Title = "p1", Description = "p1", PageOrder = 1, Fields = [] }],
            Summary = new TaskSummaryConfiguration { Mode = mode, Flows = flows }
        };
}

public class SubmitFormApplicationServiceTests
{
    private readonly IApplicationStateService _applicationState = Substitute.For<IApplicationStateService>();
    private readonly IApplicationsClient _applicationsClient = Substitute.For<IApplicationsClient>();
    private readonly IFormSessionStore _session = Substitute.For<IFormSessionStore>();
    private readonly IConditionalLogicOrchestrator _conditionalLogic = Substitute.For<IConditionalLogicOrchestrator>();
    private readonly SubmitFormApplicationService _service;
    private readonly Guid _applicationId = Guid.NewGuid();

    public SubmitFormApplicationServiceTests()
    {
        _applicationState.AreAllTasksCompleted(Arg.Any<FormTemplate>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<Guid?>(), Arg.Any<string>())
            .Returns(true);
        _applicationState.ValidateAllRequiredFieldsForSubmission(Arg.Any<FormTemplate>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<Func<string, bool>?>())
            .Returns(new Dictionary<string, List<string>>());
        _applicationsClient.GetFileValidationGateAsync(Arg.Any<Guid>())
            .Returns(new FileValidationGateDto { CanSubmit = true, BlockingFiles = [] });
        _applicationsClient.SubmitApplicationAsync(Arg.Any<Guid>())
            .Returns(new ApplicationDto { ApplicationReference = "REF-1" });
        _service = new SubmitFormApplicationService(
            _applicationState,
            _applicationsClient,
            _session,
            _conditionalLogic,
            NullLogger<SubmitFormApplicationService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStayOnPage_WhenNotAllTasksAreComplete()
    {
        _applicationState.AreAllTasksCompleted(Arg.Any<FormTemplate>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<Guid?>(), Arg.Any<string>())
            .Returns(false);

        var result = await _service.ExecuteAsync(EditableState());

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(FormState.ApplicationPreview, result.FormState);
        Assert.Contains(result.Errors, e => e.Message.Contains("All sections must be completed"));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStayOnPage_WhenFileValidationGateBlocksSubmit()
    {
        _applicationsClient.GetFileValidationGateAsync(Arg.Any<Guid>())
            .Returns(new FileValidationGateDto
            {
                CanSubmit = false,
                BlockingFiles = [new FileValidationBlockDto { OriginalFileName = "scan.pdf" }]
            });

        var result = await _service.ExecuteAsync(EditableState());

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message.Contains("scan.pdf"));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirect_WhenSubmitSucceeds()
    {
        var result = await _service.ExecuteAsync(EditableState());

        Assert.Equal(FormEngineOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal("/Applications/ApplicationSubmitted", result.PageName);
        await _applicationsClient.Received().SubmitApplicationAsync(_applicationId);
    }

    private FormEngineWorkState EditableState() =>
        new()
        {
            ReferenceNumber = "REF-1",
            ApplicationId = _applicationId,
            ApplicationStatus = "InProgress",
            IsEditable = true,
            Template = new FormTemplate
            {
                TemplateId = "tpl",
                TemplateName = "tpl",
                Description = "tpl",
                TaskGroups = []
            },
            FormData = new Dictionary<string, object>()
        };
}

public class SaveFormPageServiceTests
{
    private readonly ITemplateManagementService _templates = Substitute.For<ITemplateManagementService>();
    private readonly IFormValidationOrchestrator _validation = Substitute.For<IFormValidationOrchestrator>();
    private readonly IFormNavigationService _navigation = Substitute.For<IFormNavigationService>();
    private readonly IConditionalLogicOrchestrator _conditionalLogic = Substitute.For<IConditionalLogicOrchestrator>();
    private readonly SaveFormPageService _service;

    public SaveFormPageServiceTests()
    {
        _validation.ValidatePage(default!, default!, default).ReturnsForAnyArgs(FormValidationResult.Success);
        _conditionalLogic.ApplyConditionalLogicAsync(default!, default!, default)
            .ReturnsForAnyArgs(new FormConditionalState());
        _conditionalLogic.GetNextPageAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs((string?)null);
        _navigation.GetTaskSummaryUrl(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => $"/applications/{call.ArgAt<string>(1)}/{call.ArgAt<string>(0)}");
        _service = new SaveFormPageService(
            _templates,
            new PostedFormDataBinder(),
            Substitute.For<IFormFileFieldService>(),
            _validation,
            Substitute.For<IApplicationResponseService>(),
            Substitute.For<ICollectionFlowProgressStore>(),
            Substitute.For<IFormSessionStore>(),
            Substitute.For<INavigationHistoryService>(),
            _navigation,
            Substitute.For<IFormStateManager>(),
            _conditionalLogic,
            Substitute.For<IComplexFieldConfigurationService>(),
            Substitute.For<IDerivedCollectionFlowService>(),
            Substitute.For<IApplicationStateService>(),
            NullLogger<SaveFormPageService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStayOnPage_WhenNotEditable()
    {
        var result = await _service.ExecuteAsync(PageState(), new Dictionary<string, IReadOnlyList<string>>(), null);

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message == FormEngineMessages.NoWritePermission);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStayOnPage_WhenValidationFails()
    {
        var page = new PageModel { PageId = "p1", Slug = "p1", Title = "p1", Description = "p1", PageOrder = 1, Fields = [] };
        var task = TaskWithPages(page);
        Register(task, page);
        _validation.ValidatePage(default!, default!, default)
            .ReturnsForAnyArgs(new FormValidationResult([new FormValidationError("name", "Enter a name")]));

        var state = PageState();
        state.IsEditable = true;
        state.CurrentPageId = "p1";
        state.TaskId = task.TaskId;
        var result = await _service.ExecuteAsync(state, new Dictionary<string, IReadOnlyList<string>>(), null);

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message == "Enter a name");
        Assert.True(result.PersistErrors);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectToNextPage_WhenCurrentPageIsNotLast()
    {
        var first = new PageModel { PageId = "p1", Slug = "p1", Title = "p1", Description = "p1", PageOrder = 1, Fields = [], ReturnToSummaryPage = false };
        var second = new PageModel { PageId = "p2", Slug = "p2", Title = "p2", Description = "p2", PageOrder = 2, Fields = [], ReturnToSummaryPage = false };
        var task = TaskWithPages(first, second);
        Register(task, first);

        var state = PageState();
        state.IsEditable = true;
        state.CurrentPageId = "p1";
        state.TaskId = task.TaskId;
        var result = await _service.ExecuteAsync(state, new Dictionary<string, IReadOnlyList<string>>(), null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}/p2", result.RedirectUrl);
    }

    private void Register(TaskModel task, PageModel page)
    {
        var group = new TaskGroup { GroupId = "g1", GroupName = "g", GroupOrder = 1, GroupStatus = "NotStarted", Tasks = [task] };
        _templates.FindTask(Arg.Any<FormTemplate>(), Arg.Any<string>()).Returns((group, task));
        _templates.FindPage(Arg.Any<FormTemplate>(), Arg.Any<string>()).Returns((group, task, page));
    }

    private static FormEngineWorkState PageState() =>
        new()
        {
            ReferenceNumber = "REF-1",
            ApplicationId = Guid.NewGuid(),
            Template = new FormTemplate { TemplateId = "tpl", TemplateName = "tpl", Description = "tpl", TaskGroups = [] },
            FormData = new Dictionary<string, object>(),
            Data = new Dictionary<string, object>()
        };

    private static TaskModel TaskWithPages(params PageModel[] pages) =>
        new()
        {
            TaskId = "t1",
            TaskName = "About you",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [.. pages]
        };
}

public class PrepareFormEngineGetServiceTests
{
    private readonly ITemplateManagementService _templates = Substitute.For<ITemplateManagementService>();
    private readonly IApplicationResponseService _responses = Substitute.For<IApplicationResponseService>();
    private readonly IApplicationsClient _applications = Substitute.For<IApplicationsClient>();
    private readonly PrepareFormEngineGetService _service;

    public PrepareFormEngineGetServiceTests()
    {
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object> { ["name"] = "Ada" });
        _applications.GetFilesForApplicationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ObservableCollection<UploadDto>());
        _applications.GetFileValidationGateAsync(Arg.Any<Guid>())
            .Returns(new FileValidationGateDto { CanSubmit = true, BlockingFiles = [] });
        _service = new PrepareFormEngineGetService(
            _templates,
            _responses,
            Substitute.For<ICollectionFlowProgressStore>(),
            Substitute.For<IFormSessionStore>(),
            Substitute.For<IConditionalLogicOrchestrator>(),
            Substitute.For<IFormStateManager>(),
            Substitute.For<IFormFileFieldService>(),
            Substitute.For<IComplexFieldConfigurationService>(),
            Substitute.For<IDerivedCollectionFlowService>(),
            _applications,
            Substitute.For<INavigationHistoryService>(),
            Substitute.For<IApplicationStateService>(),
            NullLogger<PrepareFormEngineGetService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseDummyTemplate_WhenTemplateIsMissing()
    {
        var state = new FormEngineWorkState { ReferenceNumber = "REF-1", Data = new Dictionary<string, object>() };
        var result = await _service.ExecuteAsync(state, isPreview: false, isBackNav: false, isEditable: true);

        Assert.Equal("dummy", state.Template!.TemplateId);
        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal("Ada", state.Data["name"]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirect_WhenNotEditableAndPageIsPresent()
    {
        var state = new FormEngineWorkState
        {
            ReferenceNumber = "REF-1",
            CurrentPageId = "p1",
            Template = new FormTemplate { TemplateId = "tpl", TemplateName = "tpl", Description = "tpl", TaskGroups = [] },
            Data = new Dictionary<string, object>()
        };

        var result = await _service.ExecuteAsync(state, isPreview: false, isBackNav: false, isEditable: false);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal("~/applications/REF-1", result.RedirectUrl);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldOverlayDatabaseValidationStatus_OnPreviewFormData()
    {
        var fileId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var pendingJson = JsonSerializer.Serialize(new List<UploadDto>
        {
            new()
            {
                Id = fileId,
                OriginalFileName = "scan.xlsx",
                ValidationStatus = FileValidationStatus.Pending
            }
        });
        _applications.GetFilesForApplicationAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(new ObservableCollection<UploadDto>
            {
                new()
                {
                    Id = fileId,
                    OriginalFileName = "scan.xlsx",
                    ValidationStatus = FileValidationStatus.Passed,
                    ValidationMessage = "OK"
                }
            });
        _applications.GetFileValidationGateAsync(applicationId)
            .Returns(new FileValidationGateDto { CanSubmit = true, BlockingFiles = [] });

        var state = new FormEngineWorkState
        {
            ReferenceNumber = "REF-1",
            ApplicationId = applicationId,
            Template = new FormTemplate { TemplateId = "tpl", TemplateName = "tpl", Description = "tpl", TaskGroups = [] },
            FormData = new Dictionary<string, object> { ["evidence"] = pendingJson },
            Data = new Dictionary<string, object>()
        };

        var result = await _service.ExecuteAsync(state, isPreview: true, isBackNav: false, isEditable: true);

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.False(result.FileValidationBlocksSubmit);
        var uploads = JsonSerializer.Deserialize<List<UploadDto>>(state.FormData["evidence"].ToString()!);
        Assert.Equal(FileValidationStatus.Passed, uploads![0].ValidationStatus);
        Assert.Equal("OK", uploads[0].ValidationMessage);
        _responses.Received().StoreFormDataInSession(state.FormData);
    }
}

public class RemoveCollectionItemServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldBadRequest_WhenIdsAreMissing()
    {
        var service = new RemoveCollectionItemService(
            Substitute.For<ITemplateManagementService>(),
            Substitute.For<IApplicationResponseService>(),
            Substitute.For<IFileUploadService>(),
            Substitute.For<IFormNavigationService>(),
            NullLogger<RemoveCollectionItemService>.Instance);

        var result = await service.ExecuteAsync(new FormEngineWorkState { IsEditable = true }, "", "i1", "f1", true);

        Assert.Equal(FormEngineOutcomeKind.BadRequest, result.Kind);
        Assert.Equal(FormEngineMessages.FieldIdAndItemIdRequired, result.ErrorMessage);
    }
}

public class UploadFormFileServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldStayWithError_WhenNoFileIsPosted()
    {
        var files = Substitute.For<IFormFileFieldService>();
        files.GetFiles(Arg.Any<FormFileFieldContext>(), Arg.Any<string>()).Returns([]);
        var service = new UploadFormFileService(
            files,
            Substitute.For<IFileUploadService>(),
            Substitute.For<IInfectedUploadFilter>(),
            NullLogger<UploadFormFileService>.Instance);

        var result = await service.ExecuteAsync(
            new FormEngineWorkState(),
            new UploadFormFileRequest(Guid.NewGuid(), "evidence", null, null, Stream.Null, "cv.pdf", "application/pdf", "ctx", false));

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(FormEngineMessages.SelectAFile, result.ErrorMessage);
        Assert.Contains(result.Errors, e => e.FieldKey == "UploadFile");
    }
}
