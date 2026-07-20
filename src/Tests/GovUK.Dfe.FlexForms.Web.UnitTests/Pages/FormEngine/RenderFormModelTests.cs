using AutoFixture;
using AutoFixture.AutoNSubstitute;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Pages.FormEngine;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
    private readonly IApplicationResponseService _applicationResponseService;
    private readonly INavigationHistoryService _navigationHistoryService;
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
        
        _session = _fixture.Create<ISession>();
        _fixture.Register(() => _session);

        var applicationStateService = _fixture.Create<IApplicationStateService>();
        applicationStateService.IsApplicationEditable(Arg.Any<string>()).Returns(true);
        _fixture.Register(() => applicationStateService);
        
        _applicationResponseService = _fixture.Create<IApplicationResponseService>();
        _fixture.Register(() => _applicationResponseService);

        _navigationHistoryService = _fixture.Create<INavigationHistoryService>();
        _fixture.Register(() => _navigationHistoryService);

        var request = _fixture.Create<HttpRequest>();
        request.Path = PathString.Empty;
        request.QueryString = QueryString.Empty;
        _fixture.Register(() => request);

        _model = _fixture.Create<RenderFormModel>();
    }

    [Theory]
    [InlineData("flow/some-page")]
    [InlineData("some-other-page")]
    public async Task OnGetAsync_loads_accumulated_form_data_from_session(string currentPageId)
    {
        var expectedData = new Dictionary<string, object> { { "someField", "someValue" } };
        _applicationResponseService.GetAccumulatedFormData(Arg.Any<ISession>()).Returns(expectedData);
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
        var flow = _fixture.Build<MultiCollectionFlowConfiguration>()
            .With(f => f.FlowId, flowId)
            .With(f => f.Pages, [firstPage, lastPage])
            .Create();
        var summary = _fixture.Build<TaskSummaryConfiguration>()
            .With(s => s.Flows, [flow])
            .Create();
        var task = _fixture
            .Build<TaskModel>()
            .With(t => t.TaskId, _model.TaskId)
            .With(t => t.Summary, summary)
            .Create();
        _fixture.Register(() => task);

        await _model.OnPostPageAsync();

        var expectedScope = $"{_model.ReferenceNumber}:{_model.TaskId}:flow:{flowId}:{instanceId}";

        _navigationHistoryService.Received().Clear(expectedScope, Arg.Any<ISession>());
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
        var flow = _fixture.Build<MultiCollectionFlowConfiguration>()
            .With(f => f.FlowId, flowId)
            .With(f => f.Pages, [firstPage, lastPage])
            .Create();
        var summary = _fixture.Build<TaskSummaryConfiguration>()
            .With(s => s.Flows, [flow])
            .Create();
        var task = _fixture
            .Build<TaskModel>()
            .With(t => t.TaskId, _model.TaskId)
            .With(t => t.Summary, summary)
            .Create();
        _fixture.Register(() => task);

        await _model.OnPostPageAsync();

        var expectedScope = $"{_model.ReferenceNumber}:{_model.TaskId}:flow:{flowId}:{instanceId}";
        var expectedUrl =
            $"/applications/{_model.ReferenceNumber}/{_model.TaskId}/flow/{flowId}/{instanceId}/{flowPageId}";

        _navigationHistoryService.Received().Push(expectedScope, expectedUrl, Arg.Any<ISession>());
        _navigationHistoryService.DidNotReceive().Clear(Arg.Any<string>(), Arg.Any<ISession>());
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

        var flow = _fixture.Build<MultiCollectionFlowConfiguration>()
            .With(f => f.FlowId, flowId)
            .With(f => f.AddItemMessage, "{firstField} has been added")
            .With(f => f.UpdateItemMessage, "{firstField} has been updated")
            .Create();
        var summary = _fixture.Build<TaskSummaryConfiguration>()
            .With(s => s.Flows, [flow])
            .Create();
        var task = _fixture
            .Build<TaskModel>()
            .With(t => t.TaskId, _model.TaskId)
            .With(t => t.Summary, summary)
            .Create();
        _fixture.Register(() => task);

        _session.TryGetValue($"FlowProgress_{flowId}_{instanceId}", out _).Returns(call =>
        {
            call[1] = "{\"firstField\":\"Some Data\",\"secondField\":2}"u8.ToArray();
            return true;
        });

        await _model.OnPostPageAsync();
        
        Assert.NotEqual("{firstField} has been updated", _model.SuccessMessage);
        Assert.DoesNotContain("{firstField}", _model.SuccessMessage);
        Assert.NotEqual("Some Data has been updated", _model.SuccessMessage);
        Assert.Equal("Some Data has been added", _model.SuccessMessage);
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

        var flow = _fixture.Build<MultiCollectionFlowConfiguration>()
            .With(f => f.FlowId, flowId)
            .With(f => f.AddItemMessage, "{firstField} has been added")
            .With(f => f.UpdateItemMessage, "{firstField} has been updated")
            .Create();
        var summary = _fixture.Build<TaskSummaryConfiguration>()
            .With(s => s.Flows, [flow])
            .Create();
        var task = _fixture
            .Build<TaskModel>()
            .With(t => t.TaskId, _model.TaskId)
            .With(t => t.Summary, summary)
            .Create();
        _fixture.Register(() => task);

        _session.TryGetValue($"FlowProgress_{flowId}_{instanceId}", out _).Returns(call =>
        {
            call[1] = "{\"secondField\":2}"u8.ToArray();
            return true;
        });
        _applicationResponseService.GetAccumulatedFormData(Arg.Any<ISession>())
            .Returns(new Dictionary<string, object> { { flow.FieldId, $"[{{\"id\":\"{instanceId}\",\"firstField\":\"Some Data\",\"secondField\":2}}]" } });

        await _model.OnPostPageAsync();
        
        Assert.NotEqual("{firstField} has been added", _model.SuccessMessage);
        Assert.DoesNotContain("{firstField}", _model.SuccessMessage);
        Assert.NotEqual("Some Data has been added", _model.SuccessMessage);
        Assert.Equal("Some Data has been updated", _model.SuccessMessage);
    }

    [Theory]
    [InlineData("some text", "some text")]
    [InlineData("👍", "&#x1F44D;")]
    [InlineData("<script>alert('hello')</script>", "&lt;script&gt;alert(&#x27;hello&#x27;)&lt;/script&gt;")]
    public async Task OnPostPageAsync_sanitises_form_data(string formValue, string expectedSavedData)
    {
        var request = _fixture.Create<HttpRequest>();
        request.Form = new FormCollection(new Dictionary<string, StringValues> { { "Data[someField]", formValue } });
        _fixture.Register(() => request);

        await _model.OnPostPageAsync();

        Assert.Equal(expectedSavedData, _model.Data["someField"]);
    }
}