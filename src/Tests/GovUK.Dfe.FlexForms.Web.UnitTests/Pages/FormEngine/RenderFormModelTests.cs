using System.Security.Claims;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Pages.FormEngine;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Task = System.Threading.Tasks.Task;
using PageModel = GovUK.Dfe.FlexForms.Domain.Models.Page;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.FormEngine;

public class RenderFormModelTests
{
    private readonly IFixture _fixture;
    private readonly ISession _session;
    private readonly HttpRequest _request;
    private readonly IApplicationResponseService _applicationResponseService;
    private readonly INavigationHistoryService _navigationHistoryService;
    private readonly ITemplateManagementService _templateManagementService;
    private readonly RenderFormModel _model;

    public RenderFormModelTests()
    {
        _fixture = new Fixture().Customize(new AutoNSubstituteCustomization { ConfigureMembers = true });

        _fixture.Customize<Condition>(ob => ob.Without(rule => rule.Conditions));
        _fixture.Customize<CompiledPageActionDescriptor>(ob => ob
            .Without(desc => desc.HandlerMethods)
            .Without(desc => desc.Parameters)
            .Without(desc => desc.BoundProperties)
        );
        _fixture.Customize<ActionDescriptor>(ob => ob
            .Without(desc => desc.Parameters)
            .Without(desc => desc.BoundProperties)
        );

        _session = Substitute.For<ISession>();
        _session.TryGetValue(Arg.Any<string>(), out Arg.Any<byte[]?>()).Returns(false);
        _session.Keys.Returns(Array.Empty<string>());
        _fixture.Register(() => _session);

        var applicationId = Guid.NewGuid();
        var applicationStateService = Substitute.For<IApplicationStateService>();
        applicationStateService.IsApplicationEditable(Arg.Any<string>()).Returns(true);
        applicationStateService.EnsureApplicationIdAsync(Arg.Any<string>())
            .Returns((applicationId, (ApplicationDto?)null));
        applicationStateService.GetApplicationStatus(Arg.Any<Guid?>()).Returns("InProgress");
        _fixture.Register(() => applicationStateService);

        _applicationResponseService = Substitute.For<IApplicationResponseService>();
        _applicationResponseService.GetAccumulatedFormData().Returns(new Dictionary<string, object>());
        _fixture.Register(() => _applicationResponseService);

        _navigationHistoryService = Substitute.For<INavigationHistoryService>();
        _fixture.Register(() => _navigationHistoryService);

        _templateManagementService = Substitute.For<ITemplateManagementService>();
        _templateManagementService.LoadTemplateAsync(Arg.Any<string>(), Arg.Any<ApplicationDto?>())
            .Returns(new FormTemplate
            {
                TemplateId = "template",
                TemplateName = "template",
                Description = "template",
                TaskGroups = []
            });
        _fixture.Register(() => _templateManagementService);

        var validationOrchestrator = Substitute.For<IFormValidationOrchestrator>();
        validationOrchestrator.ValidatePage(default!, default!, default).ReturnsForAnyArgs(FormValidationResult.Success);
        validationOrchestrator.ValidateTask(default!, default!, default).ReturnsForAnyArgs(FormValidationResult.Success);
        validationOrchestrator.ValidateApplication(default!, default!).ReturnsForAnyArgs(FormValidationResult.Success);
        _fixture.Register(() => validationOrchestrator);

        var infectedFileStore = Substitute.For<IInfectedFileStore>();
        infectedFileStore.IsFileInfected(Arg.Any<Guid>()).Returns(false);
        infectedFileStore.IsFileNameInfected(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        _fixture.Register(() => infectedFileStore);

        var conditionalLogic = Substitute.For<IConditionalLogicOrchestrator>();
        conditionalLogic.ApplyConditionalLogicAsync(default!, default!, default)
            .ReturnsForAnyArgs(new FormConditionalState());
        _fixture.Register(() => conditionalLogic);

        var formNavigationService = Substitute.For<IFormNavigationService>();
        formNavigationService.GetSubFlowPageUrl(default!, default!, default!, default!, default!)
            .ReturnsForAnyArgs("/applications/ref/task/flow/next");
        formNavigationService.GetCollectionFlowSummaryUrl(default!, default!)
            .ReturnsForAnyArgs("/applications/ref/task");
        formNavigationService.GetBackLinkUrl(default!, default!, default!)
            .ReturnsForAnyArgs("/back");
        _fixture.Register(() => formNavigationService);

        _request = Substitute.For<HttpRequest>();
        _request.Path = PathString.Empty;
        _request.QueryString = QueryString.Empty;
        _request.Query.Returns(new QueryCollection());
        _request.Form.Returns(new FormCollection(new Dictionary<string, StringValues>()));
        _request.Scheme.Returns("https");
        _request.Host.Returns(new HostString("localhost"));
        _fixture.Register(() => _request);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.Session.Returns(_session);
        httpContext.Request.Returns(_request);
        httpContext.Response.Returns(Substitute.For<HttpResponse>());
        httpContext.User.Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Admin")],
            authenticationType: "Test")));
        _fixture.Register(() => httpContext);
        _fixture.Register(() => new PageContext { HttpContext = httpContext });

        _model = _fixture.Create<RenderFormModel>();
        _model.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
        _model.Data = new Dictionary<string, object>();
        _model.FlowId = null;
        _model.InstanceId = null;
        _model.FlowPageId = null;
        _model.DerivedFlowId = null;
        _model.DerivedItemId = null;
        _model.DerivedPageId = null;
        _model.SuccessMessage = null;
        _model.ErrorMessage = null;
        _model.CurrentPageId = string.Empty;
        _model.ApplicationId = applicationId;
    }

    [Theory]
    [InlineData("flow/some-page")]
    [InlineData("some-other-page")]
    public async Task OnGetAsync_loads_accumulated_form_data_from_session(string currentPageId)
    {
        var expectedData = new Dictionary<string, object> { { "someField", "someValue" } };
        _applicationResponseService.GetAccumulatedFormData().Returns(expectedData);
        _model.CurrentPageId = currentPageId;

        await _model.OnGetAsync();

        var actualData = Assert.Contains("someField", _model.Data);
        Assert.Equal(expectedData["someField"], actualData);
    }

    [Fact]
    public async Task OnPostPageAsync_when_last_form_in_task_is_submitted_then_clear_navigation_history_for_scope()
    {
        var flowId = _fixture.Create<string>();
        var instanceId = _fixture.Create<string>();
        var flowPageId = _fixture.Create<string>();

        _model.ReferenceNumber = _fixture.Create<string>();
        _model.TaskId = _fixture.Create<string>();
        _model.CurrentPageId = $"flow/{flowId}/{instanceId}/{flowPageId}";

        var firstPage = _fixture.Create<PageModel>();
        var lastPage = _fixture.Build<PageModel>()
            .With(p => p.PageId, flowPageId)
            .Create();
        RegisterFlowTask(flowId, [firstPage, lastPage]);

        await _model.OnPostPageAsync();

        var expectedScope = $"{_model.ReferenceNumber}:{_model.TaskId}:flow:{flowId}:{instanceId}";

        _navigationHistoryService.Received().Clear(expectedScope);
    }

    [Fact]
    public async Task OnPostPageAsync_when_form_in_task_thats_not_the_last_one_is_submitted_then_navigation_history_for_scope_is_pushed()
    {
        var flowId = _fixture.Create<string>();
        var instanceId = _fixture.Create<string>();
        var flowPageId = _fixture.Create<string>();

        _model.ReferenceNumber = _fixture.Create<string>();
        _model.TaskId = _fixture.Create<string>();
        _model.CurrentPageId = $"flow/{flowId}/{instanceId}/{flowPageId}";

        var firstPage = _fixture.Build<PageModel>()
            .With(p => p.PageId, flowPageId)
            .Create();
        var lastPage = _fixture.Create<PageModel>();
        RegisterFlowTask(flowId, [firstPage, lastPage]);

        await _model.OnPostPageAsync();

        var expectedScope = $"{_model.ReferenceNumber}:{_model.TaskId}:flow:{flowId}:{instanceId}";
        var expectedUrl =
            $"/applications/{_model.ReferenceNumber}/{_model.TaskId}/flow/{flowId}/{instanceId}/{flowPageId}";

        _navigationHistoryService.Received().Push(expectedScope, expectedUrl);
        _navigationHistoryService.DidNotReceive().Clear(Arg.Any<string>());
    }

    [Fact]
    public async Task OnPostPageAsync_when_collection_item_is_added_then_all_fields_are_available_for_success_message()
    {
        var flowId = _fixture.Create<string>();
        var instanceId = _fixture.Create<string>();
        var flowPageId = _fixture.Create<string>();

        _model.ReferenceNumber = _fixture.Create<string>();
        _model.TaskId = _fixture.Create<string>();
        _model.CurrentPageId = $"flow/{flowId}/{instanceId}/{flowPageId}";

        var lastPage = _fixture.Build<PageModel>()
            .With(p => p.PageId, flowPageId)
            .Create();
        var task = RegisterFlowTask(flowId, [_fixture.Create<PageModel>(), lastPage], "{firstField} has been added", "{firstField} has been updated");

        _session.TryGetValue($"FlowProgress_{flowId}_{instanceId}", out _).Returns(call =>
        {
            call[1] = "{\"firstField\":\"Some Data\",\"secondField\":2}"u8.ToArray();
            return true;
        });

        await _model.OnPostPageAsync();

        Assert.Equal($"{task.TaskName} updated", _model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostPageAsync_when_collection_item_is_updated_then_all_fields_are_available_for_success_message()
    {
        var flowId = _fixture.Create<string>();
        var instanceId = _fixture.Create<string>();
        var flowPageId = _fixture.Create<string>();

        _model.ReferenceNumber = _fixture.Create<string>();
        _model.TaskId = _fixture.Create<string>();
        _model.CurrentPageId = $"flow/{flowId}/{instanceId}/{flowPageId}";

        var lastPage = _fixture.Build<PageModel>()
            .With(p => p.PageId, flowPageId)
            .Create();
        var flow = _fixture.Build<MultiCollectionFlowConfiguration>()
            .With(f => f.FlowId, flowId)
            .With(f => f.AddItemMessage, "{firstField} has been added")
            .With(f => f.UpdateItemMessage, "{firstField} has been updated")
            .With(f => f.Pages, [_fixture.Create<PageModel>(), lastPage])
            .Create();
        var task = RegisterFlowTask(flow);

        _session.TryGetValue($"FlowProgress_{flowId}_{instanceId}", out _).Returns(call =>
        {
            call[1] = "{\"secondField\":2}"u8.ToArray();
            return true;
        });
        _applicationResponseService.GetAccumulatedFormData()
            .Returns(new Dictionary<string, object> { { flow.FieldId, $"[{{\"id\":\"{instanceId}\",\"firstField\":\"Some Data\",\"secondField\":2}}]" } });

        await _model.OnPostPageAsync();

        Assert.Equal($"{task.TaskName} updated", _model.SuccessMessage);
    }

    [Theory]
    [InlineData("some text", "some text")]
    [InlineData("👍", "&#x1F44D;")]
    [InlineData("<script>alert('hello')</script>", "&lt;script&gt;alert(&#x27;hello&#x27;)&lt;/script&gt;")]
    public async Task OnPostPageAsync_sanitises_form_data(string formValue, string expectedSavedData)
    {
        _request.Form.Returns(new FormCollection(new Dictionary<string, StringValues> { { "Data[someField]", formValue } }));

        await _model.OnPostPageAsync();

        Assert.Equal(expectedSavedData, _model.Data["someField"]);
    }

    private TaskModel RegisterFlowTask(
        string flowId,
        List<PageModel> pages,
        string? addItemMessage = null,
        string? updateItemMessage = null)
    {
        var flow = _fixture.Build<MultiCollectionFlowConfiguration>()
            .With(f => f.FlowId, flowId)
            .With(f => f.Pages, pages)
            .With(f => f.AddItemMessage, addItemMessage ?? _fixture.Create<string>())
            .With(f => f.UpdateItemMessage, updateItemMessage ?? _fixture.Create<string>())
            .Create();

        return RegisterFlowTask(flow);
    }

    private TaskModel RegisterFlowTask(MultiCollectionFlowConfiguration flow)
    {
        var summary = _fixture.Build<TaskSummaryConfiguration>()
            .With(s => s.Flows, [flow])
            .Create();
        var task = _fixture
            .Build<TaskModel>()
            .With(t => t.TaskId, _model.TaskId)
            .With(t => t.Summary, summary)
            .Create();
        var group = _fixture.Build<TaskGroup>()
            .With(g => g.Tasks, [task])
            .Create();

        _templateManagementService.FindTask(Arg.Any<FormTemplate>(), Arg.Any<string>()).Returns((group, task));
        return task;
    }
}
