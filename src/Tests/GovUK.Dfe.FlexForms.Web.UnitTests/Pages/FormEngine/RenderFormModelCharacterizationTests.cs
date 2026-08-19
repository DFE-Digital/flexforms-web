using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Text.Json;
using AutoFixture;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Testing.AutoFixture.Customizations;
using GovUK.Dfe.CoreLibs.Testing.Helpers;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Pages.FormEngine;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Task = System.Threading.Tasks.Task;
using PageModel = GovUK.Dfe.FlexForms.Domain.Models.Page;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.FormEngine;

public class RenderFormModelCharacterizationTests
{
    private readonly IFixture _fixture;
    private readonly HttpRequest _request;
    private readonly ITemplateManagementService _templates;
    private readonly IFormValidationOrchestrator _validation;
    private readonly IApplicationStateService _applicationState;
    private readonly IFieldRequirementService _fieldRequirements;
    private readonly IApplicationsClient _applicationsClient;
    private readonly IFileUploadService _fileUploadService;
    private readonly IFormFileFieldService _fileFieldService;
    private readonly IInfectedUploadFilter _infectedFilter;
    private readonly IApplicationResponseService _responses;
    private readonly IFormNavigationService _navigation;
    private readonly IConditionalLogicOrchestrator _conditionalLogic;
    private readonly RenderFormModel _model;
    private readonly Guid _applicationId = Guid.NewGuid();

    public RenderFormModelCharacterizationTests()
    {
        _fixture = FixtureFactoryHelper.ConfigureFixtureFactory([
            typeof(NSubstituteWithMembersCustomization),
            typeof(OmitCircularReferenceCustomization),
            typeof(RazorPageCustomization)
        ]);
        _fixture.Customize<Condition>(ob => ob.Without(rule => rule.Conditions));

        var session = Substitute.For<ISession>();
        session.TryGetValue(Arg.Any<string>(), out Arg.Any<byte[]?>()).Returns(false);
        session.Keys.Returns(Array.Empty<string>());
        _fixture.Register(() => session);

        _applicationState = Substitute.For<IApplicationStateService>();
        _applicationState.IsApplicationEditable(Arg.Any<string>()).Returns(true);
        _applicationState.EnsureApplicationIdAsync(Arg.Any<string>()).Returns((_applicationId, (ApplicationDto?)null));
        _applicationState.GetApplicationStatus(Arg.Any<Guid?>()).Returns("InProgress");
        _applicationState.AreAllTasksCompleted(Arg.Any<FormTemplate>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<Guid?>(), Arg.Any<string>())
            .Returns(true);
        _applicationState.ValidateAllRequiredFieldsForSubmission(Arg.Any<FormTemplate>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<Func<string, bool>?>())
            .Returns(new Dictionary<string, List<string>>());
        _fixture.Register(() => _applicationState);

        _responses = Substitute.For<IApplicationResponseService>();
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object>());
        _fixture.Register(() => _responses);

        _templates = Substitute.For<ITemplateManagementService>();
        _templates.LoadTemplateAsync(Arg.Any<string>(), Arg.Any<ApplicationDto?>())
            .Returns(EmptyTemplate());
        _fixture.Register(() => _templates);

        _validation = Substitute.For<IFormValidationOrchestrator>();
        _validation.ValidatePage(default!, default!, default).ReturnsForAnyArgs(FormValidationResult.Success);
        _validation.ValidateTask(default!, default!, default).ReturnsForAnyArgs(FormValidationResult.Success);
        _validation.ValidateApplication(default!, default!).ReturnsForAnyArgs(FormValidationResult.Success);
        _fixture.Register(() => _validation);

        _fixture.Register(() => Substitute.For<IInfectedFileStore>());
        var sessionStore = Substitute.For<IFormSessionStore>();
        _fixture.Register(() => sessionStore);
        _fixture.Register<IPostedFormDataBinder>(() => new PostedFormDataBinder());
        _fixture.Register<ICollectionFlowProgressStore>(() => new CollectionFlowProgressStore(sessionStore));

        _infectedFilter = Substitute.For<IInfectedUploadFilter>();
        _infectedFilter.FilterList(Arg.Any<IReadOnlyList<UploadDto>>(), Arg.Any<string?>())
            .Returns(call => (call.Arg<IReadOnlyList<UploadDto>>() ?? []).ToList());
        _infectedFilter.FilterUploadDataJson(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(call => call.ArgAt<string?>(0) ?? string.Empty);
        _fixture.Register(() => _infectedFilter);

        _fileFieldService = Substitute.For<IFormFileFieldService>();
        _fileFieldService.GetFiles(Arg.Any<FormFileFieldContext>(), Arg.Any<string>()).Returns(Array.Empty<UploadDto>());
        _fixture.Register(() => _fileFieldService);

        _conditionalLogic = Substitute.For<IConditionalLogicOrchestrator>();
        _conditionalLogic.ApplyConditionalLogicAsync(default!, default!, default)
            .ReturnsForAnyArgs(new FormConditionalState());
        _conditionalLogic.GetNextPageAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs((string?)null);
        _fixture.Register(() => _conditionalLogic);

        var formStateManager = Substitute.For<IFormStateManager>();
        formStateManager.GetCurrentState(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(call =>
            {
                var taskId = call.ArgAt<string>(1);
                var pageId = call.ArgAt<string>(2);
                if (FormStepPolicy.IsCollectionFlowPage(pageId)) return FormState.SubFlowPage;
                if (FormStepPolicy.IsFormPage(pageId)) return FormState.FormPage;
                if (FormStepPolicy.IsTaskSummary(taskId, pageId)) return FormState.TaskSummary;
                return FormState.TaskList;
            });
        formStateManager.ShouldShowCollectionFlowSummary(Arg.Any<TaskModel>())
            .Returns(call => FormStepPolicy.IsCollectionFlowSummary(call.Arg<TaskModel>()));
        formStateManager.ShouldShowDerivedCollectionFlowSummary(Arg.Any<TaskModel>())
            .Returns(call => FormStepPolicy.IsDerivedCollectionFlowSummary(call.Arg<TaskModel>()));
        _fixture.Register(() => formStateManager);

        _navigation = Substitute.For<IFormNavigationService>();
        _navigation.GetTaskSummaryUrl(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => $"/applications/{call.ArgAt<string>(1)}/{call.ArgAt<string>(0)}");
        _navigation.GetCollectionFlowSummaryUrl(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => $"/applications/{call.ArgAt<string>(1)}/{call.ArgAt<string>(0)}");
        _navigation.GetSubFlowPageUrl(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => $"/applications/{call.ArgAt<string>(1)}/{call.ArgAt<string>(0)}/flow/{call.ArgAt<string>(2)}/{call.ArgAt<string>(3)}/{call.ArgAt<string>(4)}");
        _navigation.GetBackLinkUrl(default!, default!, default!).ReturnsForAnyArgs("/back");
        _fixture.Register(() => _navigation);

        _fieldRequirements = Substitute.For<IFieldRequirementService>();
        _fieldRequirements.GetMissingRequiredFieldsWithMessages(default!, default!, default!, default)
            .ReturnsForAnyArgs(new Dictionary<string, string>());
        _fieldRequirements.IsFieldRequired(default!, default!).ReturnsForAnyArgs(false);
        _fixture.Register(() => _fieldRequirements);

        _applicationsClient = Substitute.For<IApplicationsClient>();
        _applicationsClient.GetFileValidationGateAsync(Arg.Any<Guid>())
            .Returns(new FileValidationGateDto { CanSubmit = true, BlockingFiles = [] });
        _applicationsClient.GetFilesForApplicationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ObservableCollection<UploadDto>());
        _applicationsClient.SubmitApplicationAsync(Arg.Any<Guid>())
            .Returns(new ApplicationDto { ApplicationReference = "REF-1" });
        _fixture.Register(() => _applicationsClient);

        _fileUploadService = Substitute.For<IFileUploadService>();
        _fixture.Register(() => _fileUploadService);

        _fixture.Register(() => Substitute.For<INavigationHistoryService>());

        _fixture.Register<ICompleteFormTask>(() => new CompleteFormTaskService(
            _applicationState,
            _fieldRequirements,
            _conditionalLogic,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CompleteFormTaskService>.Instance));
        _fixture.Register<ISubmitFormApplication>(() => new SubmitFormApplicationService(
            _applicationState,
            _applicationsClient,
            sessionStore,
            _conditionalLogic,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SubmitFormApplicationService>.Instance));
        _fixture.Register<IPrepareFormEngineGet>(() => new PrepareFormEngineGetService(
            _templates,
            _responses,
            _fixture.Create<ICollectionFlowProgressStore>(),
            sessionStore,
            _conditionalLogic,
            _fixture.Create<IFormStateManager>(),
            _fileFieldService,
            _fixture.Create<IComplexFieldConfigurationService>(),
            _fixture.Create<IDerivedCollectionFlowService>(),
            _applicationsClient,
            _fixture.Create<INavigationHistoryService>(),
            _applicationState,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PrepareFormEngineGetService>.Instance));
        _fixture.Register<ISaveFormPage>(() => new SaveFormPageService(
            _templates,
            _fixture.Create<IPostedFormDataBinder>(),
            _fileFieldService,
            _validation,
            _responses,
            _fixture.Create<ICollectionFlowProgressStore>(),
            sessionStore,
            _fixture.Create<INavigationHistoryService>(),
            _navigation,
            _fixture.Create<IFormStateManager>(),
            _conditionalLogic,
            _fixture.Create<IComplexFieldConfigurationService>(),
            _fixture.Create<IDerivedCollectionFlowService>(),
            _applicationState,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SaveFormPageService>.Instance));
        _fixture.Register<IRemoveCollectionItem>(() => new RemoveCollectionItemService(
            _templates,
            _responses,
            _fileUploadService,
            _navigation,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RemoveCollectionItemService>.Instance));
        _fixture.Register<IUploadFormFile>(() => new UploadFormFileService(
            _fileFieldService,
            _fileUploadService,
            _infectedFilter,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UploadFormFileService>.Instance));
        _fixture.Register<IDeleteFormFile>(() => new DeleteFormFileService(
            _fileFieldService,
            _fileUploadService,
            _responses,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeleteFormFileService>.Instance));
        _fixture.Register<IDownloadFormFile>(() => new DownloadFormFileService(
            _fileUploadService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DownloadFormFileService>.Instance));

        _request = Substitute.For<HttpRequest>();
        _request.Path = PathString.Empty;
        _request.QueryString = QueryString.Empty;
        _request.Query.Returns(new QueryCollection());
        _request.Form.Returns(new FormCollection(new Dictionary<string, StringValues>()));
        _request.Scheme.Returns("https");
        _request.Host.Returns(new HostString("localhost"));
        _fixture.Register(() => _request);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.Session.Returns(session);
        httpContext.Request.Returns(_request);
        httpContext.Response.Returns(Substitute.For<HttpResponse>());
        httpContext.User.Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Admin")],
            authenticationType: "Test")));
        _fixture.Register(() => httpContext);

        _model = _fixture.Create<RenderFormModel>();
        _model.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
        _model.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
        _model.Data = new Dictionary<string, object>();
        _model.ReferenceNumber = "REF-1";
        _model.ApplicationId = _applicationId;
        _model.CurrentPageId = string.Empty;
        _model.TaskId = string.Empty;
    }

    [Fact]
    public async Task OnGetAsync_ShouldShowTaskList_WhenNoTaskOrPageIsPresent()
    {
        await _model.OnGetAsync();
        Assert.Equal(FormState.TaskList, _model.CurrentFormState);
    }

    [Fact]
    public async Task OnGetAsync_ShouldUseDummyTemplate_WhenTemplateIsMissing()
    {
        _templates.LoadTemplateAsync(Arg.Any<string>(), Arg.Any<ApplicationDto?>())
            .Returns((FormTemplate)null!);

        await _model.OnGetAsync();

        Assert.Equal("dummy", _model.Template.TemplateId);
        Assert.Equal(FormState.TaskList, _model.CurrentFormState);
    }

    [Fact]
    public async Task OnGetAsync_ShouldShowTaskList_WhenApplicationIdIsMissing()
    {
        _applicationState.EnsureApplicationIdAsync(Arg.Any<string>())
            .Returns(((Guid?)null, (ApplicationDto?)null));

        await _model.OnGetAsync();

        Assert.Null(_model.ApplicationId);
        Assert.Equal(FormState.TaskList, _model.CurrentFormState);
    }

    [Fact]
    public async Task OnGetAsync_ShouldShowTaskSummary_WhenOnlyTaskIdIsPresent()
    {
        var task = StandardTask();
        RegisterTask(task);
        _model.TaskId = task.TaskId;

        await _model.OnGetAsync();

        Assert.Equal(FormState.TaskSummary, _model.CurrentFormState);
        Assert.Equal(task.TaskId, _model.CurrentTask.TaskId);
    }

    [Fact]
    public async Task OnGetAsync_ShouldShowFormPage_WhenPageIdIsPresent()
    {
        var page = Page("p1", ReturnToSummary: false);
        var task = StandardTask(pages: [page]);
        RegisterTask(task, page);
        _model.TaskId = task.TaskId;
        _model.CurrentPageId = "p1";

        await _model.OnGetAsync();

        Assert.Equal(FormState.FormPage, _model.CurrentFormState);
        Assert.Equal("p1", _model.CurrentPage.PageId);
    }

    [Fact]
    public async Task OnGetAsync_ShouldShowFormPage_WhenCollectionFlowRouteIsPresent()
    {
        var flowPage = Page("fp1");
        var flow = new MultiCollectionFlowConfiguration
        {
            FlowId = "f1",
            FieldId = "members",
            Pages = [flowPage]
        };
        var task = StandardTask(mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        RegisterTask(task, flowPage);
        _model.TaskId = task.TaskId;
        _model.CurrentPageId = "flow/f1/i1/fp1";

        await _model.OnGetAsync();

        Assert.Equal(FormState.FormPage, _model.CurrentFormState);
        Assert.Equal("fp1", _model.CurrentPage.PageId);
        Assert.Equal("f1", _model.FlowId);
    }

    [Fact]
    public async Task OnGetAsync_ShouldShowFormPage_WhenDerivedFlowRouteIsPresent()
    {
        var derivedPage = Page("dp1");
        var derived = new DerivedCollectionFlowConfiguration
        {
            FlowId = "df1",
            FieldId = "decls",
            SourceFieldId = "orgs",
            Pages = [derivedPage]
        };
        var task = StandardTask(mode: FormStepPolicy.DerivedCollectionFlowMode, derivedFlows: [derived]);
        RegisterTask(task, derivedPage);
        _model.TaskId = task.TaskId;
        _model.CurrentPageId = "df1/derived/item1/dp1";

        await _model.OnGetAsync();

        Assert.Equal(FormState.FormPage, _model.CurrentFormState);
        Assert.Equal("dp1", _model.CurrentPage.PageId);
    }

    [Fact]
    public async Task OnGetAsync_ShouldShowPreview_WhenPreviewQueryIsPresent()
    {
        _request.Query.Returns(new QueryCollection(new Dictionary<string, StringValues> { ["preview"] = "true" }));
        _model.TaskId = "t1";
        _model.CurrentPageId = "p1";

        await _model.OnGetAsync();

        Assert.Equal(FormState.ApplicationPreview, _model.CurrentFormState);
    }

    [Fact]
    public async Task OnPostPageAsync_ShouldReturnPage_WhenValidationFails()
    {
        var page = Page("p1", ReturnToSummary: false);
        var task = StandardTask(pages: [page]);
        RegisterTask(task, page);
        _model.TaskId = task.TaskId;
        _model.CurrentPageId = "p1";
        _validation.ValidatePage(default!, default!, default)
            .ReturnsForAnyArgs(new FormValidationResult([new FormValidationError("name", "Enter a name")]));

        var result = await _model.OnPostPageAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(_model.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostPageAsync_ShouldRedirectToNextPage_WhenCurrentPageIsNotLast()
    {
        var first = Page("p1", ReturnToSummary: false);
        var second = Page("p2", ReturnToSummary: false);
        var task = StandardTask(pages: [first, second]);
        RegisterTask(task, first);
        _model.TaskId = task.TaskId;
        _model.CurrentPageId = "p1";

        var result = Assert.IsType<RedirectResult>(await _model.OnPostPageAsync());

        Assert.Equal($"/applications/REF-1/{task.TaskId}/p2", result.Url);
    }

    [Fact]
    public async Task OnPostPageAsync_ShouldRedirectToConditionalPage_WhenLogicSkipsTheNextPage()
    {
        var first = Page("p1", ReturnToSummary: false);
        var skipped = Page("p2", ReturnToSummary: false);
        var target = Page("p3", ReturnToSummary: false);
        var task = StandardTask(pages: [first, skipped, target]);
        RegisterTask(task, first);
        _model.TaskId = task.TaskId;
        _model.CurrentPageId = "p1";
        _conditionalLogic.GetNextPageAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs("p3");

        var result = Assert.IsType<RedirectResult>(await _model.OnPostPageAsync());

        Assert.Equal($"/applications/REF-1/{task.TaskId}/p3", result.Url);
    }

    [Fact]
    public async Task OnPostPageAsync_ShouldRedirectToTaskSummary_WhenReturnToSummaryPageIsTrue()
    {
        var page = Page("p1", ReturnToSummary: true);
        var later = Page("p2", ReturnToSummary: false);
        var task = StandardTask(pages: [page, later]);
        RegisterTask(task, page);
        _model.TaskId = task.TaskId;
        _model.CurrentPageId = "p1";

        var result = Assert.IsType<RedirectResult>(await _model.OnPostPageAsync());

        Assert.Equal($"/applications/REF-1/{task.TaskId}", result.Url);
    }

    [Fact]
    public async Task OnPostPageAsync_ShouldRestoreConfirmedFormData_WhenConfirmedQueryIsPresent()
    {
        _request.Query.Returns(new QueryCollection(new Dictionary<string, StringValues> { ["confirmed"] = "true" }));
        _model.TempData["ConfirmedFormData"] = JsonSerializer.Serialize(new Dictionary<string, object> { ["restored"] = "yes" });
        _model.TempData["ConfirmedHandler"] = "Page";

        await _model.OnPostPageAsync();

        Assert.Equal("yes", _model.Data["restored"]?.ToString());
    }

    [Fact]
    public async Task OnPostTaskSummaryAsync_ShouldReturnPage_WhenRequiredFieldsAreMissing()
    {
        var task = StandardTask();
        RegisterTask(task);
        _model.TaskId = task.TaskId;
        _model.IsTaskCompleted = true;
        _fieldRequirements.GetMissingRequiredFieldsWithMessages(default!, default!, default!, default)
            .ReturnsForAnyArgs(new Dictionary<string, string> { ["name"] = "Enter a name" });

        var result = await _model.OnPostTaskSummaryAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(_model.IsTaskCompleted);
        Assert.Equal(FormState.TaskSummary, _model.CurrentFormState);
        Assert.Contains(_model.ModelState.Values.SelectMany(v => v.Errors), e => e.ErrorMessage.Contains("Enter a name"));
    }

    [Fact]
    public async Task OnPostTaskSummaryAsync_ShouldReturnPage_WhenCollectionMinItemsAreMissing()
    {
        var flow = new MultiCollectionFlowConfiguration
        {
            FlowId = "f1",
            FieldId = "members",
            Title = "Members",
            MinItems = 2,
            Pages = [Page("fp1")]
        };
        var task = StandardTask(mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        RegisterTask(task);
        _model.TaskId = task.TaskId;
        _model.IsTaskCompleted = true;
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object>
        {
            ["members"] = """[{"id":"i1"}]"""
        });

        var result = await _model.OnPostTaskSummaryAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains(
            _model.ModelState.Values.SelectMany(v => v.Errors),
            e => e.ErrorMessage.Contains("Add at least 2 item(s) to Members"));
    }

    [Fact]
    public async Task OnPostSubmitApplicationAsync_ShouldReturnPage_WhenNotAllTasksAreComplete()
    {
        _applicationState.AreAllTasksCompleted(Arg.Any<FormTemplate>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<Guid?>(), Arg.Any<string>())
            .Returns(false);

        var result = await _model.OnPostSubmitApplicationAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(FormState.ApplicationPreview, _model.CurrentFormState);
        Assert.Contains(
            _model.ModelState.Values.SelectMany(v => v.Errors),
            e => e.ErrorMessage.Contains("All sections must be completed"));
    }

    [Fact]
    public async Task OnPostSubmitApplicationAsync_ShouldReturnPage_WhenFileValidationGateBlocksSubmit()
    {
        _applicationsClient.GetFileValidationGateAsync(Arg.Any<Guid>())
            .Returns(new FileValidationGateDto
            {
                CanSubmit = false,
                BlockingFiles = [new FileValidationBlockDto { OriginalFileName = "scan.pdf", ValidationStatus = FileValidationStatus.Pending }]
            });

        var result = await _model.OnPostSubmitApplicationAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains(
            _model.ModelState.Values.SelectMany(v => v.Errors),
            e => e.ErrorMessage.Contains("scan.pdf"));
    }

    [Fact]
    public async Task OnPostSubmitApplicationAsync_ShouldRedirect_WhenSubmitSucceeds()
    {
        var result = Assert.IsType<RedirectToPageResult>(await _model.OnPostSubmitApplicationAsync());

        Assert.Equal("/Applications/ApplicationSubmitted", result.PageName);
        await _applicationsClient.Received().SubmitApplicationAsync(_applicationId);
    }

    [Fact]
    public async Task OnPostRemoveCollectionItemAsync_ShouldDeleteAssociatedFiles_WhenConfirmed()
    {
        var fileId = Guid.NewGuid();
        var flow = new MultiCollectionFlowConfiguration
        {
            FlowId = "f1",
            FieldId = "members",
            Title = "Members",
            DeleteItemMessage = "removed",
            Pages = [Page("fp1")]
        };
        var task = StandardTask(mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        RegisterTask(task);
        _model.TaskId = task.TaskId;
        _request.Query.Returns(new QueryCollection(new Dictionary<string, StringValues> { ["confirmed"] = "true" }));
        var items = JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, object>
            {
                ["id"] = "i1",
                ["files"] = JsonSerializer.Serialize(new[] { new UploadDto { Id = fileId, OriginalFileName = "cv.pdf" } })
            }
        });
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object> { ["members"] = items });

        var result = Assert.IsType<RedirectResult>(
            await _model.OnPostRemoveCollectionItemAsync("members", "i1", "f1"));

        await _fileUploadService.Received().DeleteFileAsync(fileId, _applicationId);
        _responses.Received().AccumulateFormData(Arg.Is<Dictionary<string, object>>(d =>
            d["members"].ToString()!.Contains("[]") || d["members"].ToString() == "[]"));
        Assert.Equal($"/applications/REF-1/{task.TaskId}", result.Url);
    }

    [Fact]
    public async Task OnPostUploadFileAsync_ShouldSaveFileToSession_WhenUploadSucceeds()
    {
        var uploaded = new UploadDto { Id = Guid.NewGuid(), OriginalFileName = "cv.pdf" };
        _fileUploadService.UploadFileAsync(default!, default, default, default!, default)
            .ReturnsForAnyArgs(uploaded);
        SetUploadForm("cv.pdf");

        var result = Assert.IsType<RedirectResult>(await _model.OnPostUploadFileAsync());

        Assert.Equal("/back", result.Url);
        _fileFieldService.Received().SaveFiles(
            Arg.Any<FormFileFieldContext>(),
            "evidence",
            Arg.Is<IReadOnlyList<UploadDto>>(files => files.Any(f => f.Id == uploaded.Id)));
    }

    [Fact]
    public async Task OnPostUploadFileAsync_ShouldSkipInfectedFile_WhenBlacklistFilterRemovesIt()
    {
        var uploaded = new UploadDto { Id = Guid.NewGuid(), OriginalFileName = "virus.bin" };
        _fileUploadService.UploadFileAsync(default!, default, default, default!, default)
            .ReturnsForAnyArgs(uploaded);
        _infectedFilter.FilterList(Arg.Any<IReadOnlyList<UploadDto>>(), Arg.Any<string?>())
            .Returns([]);
        SetUploadForm("virus.bin");

        await _model.OnPostUploadFileAsync();

        _fileFieldService.Received().SaveFiles(
            Arg.Any<FormFileFieldContext>(),
            "evidence",
            Arg.Is<IReadOnlyList<UploadDto>>(files => files.Count == 0));
    }

    private void SetUploadForm(string fileName)
    {
        var content = new MemoryStream([1, 2, 3]);
        IFormFile file = new FormFile(content, 0, content.Length, "UploadFile", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
        _request.Form.Returns(new FormCollection(
            new Dictionary<string, StringValues>
            {
                ["ApplicationId"] = _applicationId.ToString(),
                ["FieldId"] = "evidence",
                ["ReturnUrl"] = "/back"
            },
            new FormFileCollection { file }));
    }

    private void RegisterTask(TaskModel task, PageModel? currentPage = null)
    {
        var group = new TaskGroup
        {
            GroupId = "g1",
            GroupName = "Group",
            GroupOrder = 1,
            GroupStatus = "NotStarted",
            Tasks = [task]
        };
        _templates.LoadTemplateAsync(Arg.Any<string>(), Arg.Any<ApplicationDto?>())
            .Returns(new FormTemplate
            {
                TemplateId = "tpl",
                TemplateName = "tpl",
                Description = "tpl",
                TaskGroups = [group]
            });
        _templates.FindTask(Arg.Any<FormTemplate>(), Arg.Any<string>()).Returns((group, task));
        if (currentPage != null)
            _templates.FindPage(Arg.Any<FormTemplate>(), Arg.Any<string>()).Returns((group, task, currentPage));
    }

    private static TaskModel StandardTask(
        string mode = "standard",
        List<PageModel>? pages = null,
        List<MultiCollectionFlowConfiguration>? flows = null,
        List<DerivedCollectionFlowConfiguration>? derivedFlows = null) =>
        new()
        {
            TaskId = "t1",
            TaskName = "About you",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = pages ?? [Page("p1")],
            Summary = new TaskSummaryConfiguration
            {
                Mode = mode,
                Flows = flows,
                DerivedFlows = derivedFlows
            }
        };

    private static PageModel Page(string id, bool ReturnToSummary = true) =>
        new()
        {
            PageId = id,
            Slug = id,
            Title = id,
            Description = id,
            PageOrder = 1,
            Fields = [new Field { FieldId = "name", Type = "text", Label = new Label { Value = "Name" }, Order = 1 }],
            ReturnToSummaryPage = ReturnToSummary
        };

    private static FormTemplate EmptyTemplate() =>
        new()
        {
            TemplateId = "tpl",
            TemplateName = "tpl",
            Description = "tpl",
            TaskGroups = []
        };
}
