using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class RemoveCollectionItemServiceCoverageTests
{
    private const string FieldId = "members";
    private const string FlowId = "members-flow";
    private const string SummaryUrl = "/applications/REF-1/t1/summary";

    private readonly ITemplateManagementService _templates = Substitute.For<ITemplateManagementService>();
    private readonly IApplicationResponseService _responses = Substitute.For<IApplicationResponseService>();
    private readonly IFileUploadService _files = Substitute.For<IFileUploadService>();
    private readonly IFormNavigationService _navigation = Substitute.For<IFormNavigationService>();
    private readonly RemoveCollectionItemService _service;

    public RemoveCollectionItemServiceCoverageTests()
    {
        _navigation.GetCollectionFlowSummaryUrl(Arg.Any<string>(), Arg.Any<string>()).Returns(SummaryUrl);
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object>());
        _service = new RemoveCollectionItemService(
            _templates,
            _responses,
            _files,
            _navigation,
            NullLogger<RemoveCollectionItemService>.Instance);
    }

    // ---------------------------------------------------------------------
    // Guard clauses
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldResolveCurrentGroupAndTask_WhenTaskIdAndTemplateAreSet()
    {
        var state = State();
        var (group, task) = RegisterTask();

        await _service.ExecuteAsync(state, FieldId, "i1", null, confirmed: true);

        Assert.Same(group, state.CurrentGroup);
        Assert.Same(task, state.CurrentTask);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldBadRequest_WhenItemIdIsMissing()
    {
        var result = await _service.ExecuteAsync(State(), FieldId, "", FlowId, confirmed: true);

        Assert.Equal(FormEngineOutcomeKind.BadRequest, result.Kind);
        Assert.Equal(FormEngineMessages.FieldIdAndItemIdRequired, result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStayOnPage_WhenStateIsNotEditable()
    {
        var state = State();
        state.IsEditable = false;

        var result = await _service.ExecuteAsync(state, FieldId, "i1", FlowId, confirmed: true);

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.True(result.ClearModelState);
        Assert.Contains(result.Errors, e => e.Message == FormEngineMessages.NoWritePermission);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectWithoutRemoving_WhenRemovalIsNotConfirmed()
    {
        SetupCollection(Item("i1", "Ada"));

        var result = await _service.ExecuteAsync(State(), FieldId, "i1", FlowId, confirmed: false);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal(SummaryUrl, result.RedirectUrl);
        Assert.Null(result.SuccessMessage);
        _responses.DidNotReceive().AccumulateFormData(Arg.Any<Dictionary<string, object>>());
    }

    // ---------------------------------------------------------------------
    // Success messages
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldBuildDefaultSuccessMessage_WhenFlowHasNoCustomMessage()
    {
        RegisterTask(Flow());
        SetupCollection(Item("i1", "Ada"));

        var result = await _service.ExecuteAsync(State(), FieldId, "i1", FlowId, confirmed: true);

        Assert.Equal("Ada has been removed from members", result.SuccessMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldBuildCustomSuccessMessage_WhenFlowDefinesDeleteItemMessage()
    {
        var flow = Flow();
        flow.DeleteItemMessage = "{name} was taken off {flowTitle}";
        RegisterTask(flow);
        SetupCollection(Item("i1", "Ada"));

        var result = await _service.ExecuteAsync(State(), FieldId, "i1", FlowId, confirmed: true);

        Assert.Equal("Ada was taken off Members", result.SuccessMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotBuildSuccessMessage_WhenFlowIdIsUnknown()
    {
        RegisterTask(Flow());
        SetupCollection(Item("i1", "Ada"));

        var result = await _service.ExecuteAsync(State(), FieldId, "i1", "unknown-flow", confirmed: true);

        Assert.Null(result.SuccessMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotBuildSuccessMessage_WhenFlowIdIsNotSupplied()
    {
        RegisterTask(Flow());
        SetupCollection(Item("i1", "Ada"));

        var result = await _service.ExecuteAsync(State(), FieldId, "i1", null, confirmed: true);

        Assert.Null(result.SuccessMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotBuildSuccessMessage_WhenTaskCannotBeResolved()
    {
        SetupCollection(Item("i1", "Ada"));

        var result = await _service.ExecuteAsync(State(), FieldId, "i1", FlowId, confirmed: true);

        Assert.Null(result.SuccessMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFallBackToGenericMessage_WhenFieldIsNotInAccumulatedData()
    {
        RegisterTask(Flow());

        var result = await _service.ExecuteAsync(State(), FieldId, "i1", FlowId, confirmed: true);

        Assert.Equal("Item has been removed from members", result.SuccessMessage);
        _responses.DidNotReceive().AccumulateFormData(Arg.Any<Dictionary<string, object>>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFallBackToGenericMessage_WhenCollectionJsonIsMalformed()
    {
        RegisterTask(Flow());
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object> { [FieldId] = "{ not json" });

        var result = await _service.ExecuteAsync(State(), FieldId, "i1", FlowId, confirmed: true);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal("Item has been removed from members", result.SuccessMessage);
        _responses.DidNotReceive().AccumulateFormData(Arg.Any<Dictionary<string, object>>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFallBackToGenericMessage_WhenItemIdIsNotInCollection()
    {
        RegisterTask(Flow());
        SetupCollection(Item("i1", "Ada"));

        var result = await _service.ExecuteAsync(State(), FieldId, "i9", FlowId, confirmed: true);

        Assert.Equal("Item has been removed from members", result.SuccessMessage);
        Assert.Single(RemainingItems());
    }

    // ---------------------------------------------------------------------
    // Removal and persistence
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldRemoveItemAndPersistTheRemainder()
    {
        var state = State();
        RegisterTask(Flow());
        SetupCollection(Item("i1", "Ada"), Item("i2", "Grace"));

        var result = await _service.ExecuteAsync(state, FieldId, "i1", FlowId, confirmed: true);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        var remaining = RemainingItems();
        Assert.Equal("i2", Assert.Single(remaining)["id"].ToString());
        await _responses.Received(1).SaveApplicationResponseAsync(
            state.ApplicationId!.Value,
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotSaveToApi_WhenApplicationIdIsMissing()
    {
        var state = State();
        state.ApplicationId = null;
        SetupCollection(Item("i1", "Ada"));

        await _service.ExecuteAsync(state, FieldId, "i1", FlowId, confirmed: true);

        Assert.Empty(RemainingItems());
        await _responses.DidNotReceive().SaveApplicationResponseAsync(
            Arg.Any<Guid>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRethrow_WhenApiSaveFailsWithExternalApplicationsException()
    {
        SetupCollection(Item("i1", "Ada"));
        _responses.SaveApplicationResponseAsync(
                Arg.Any<Guid>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 500, "err", null!, null!));

        await Assert.ThrowsAsync<ExternalApplicationsException>(
            () => _service.ExecuteAsync(State(), FieldId, "i1", FlowId, confirmed: true));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSwallowUnexpectedSaveErrors_AndStillRedirect()
    {
        SetupCollection(Item("i1", "Ada"));
        _responses.SaveApplicationResponseAsync(
                Arg.Any<Guid>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("save failed"));

        var result = await _service.ExecuteAsync(State(), FieldId, "i1", FlowId, confirmed: true);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal(SummaryUrl, result.RedirectUrl);
    }

    // ---------------------------------------------------------------------
    // File cleanup
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldDeleteFilesAttachedToTheRemovedItem()
    {
        var state = State();
        var firstFile = Guid.NewGuid();
        var secondFile = Guid.NewGuid();
        SetupCollection(ItemWithFiles("i1", UploadsJson(firstFile, secondFile)));

        await _service.ExecuteAsync(state, FieldId, "i1", FlowId, confirmed: true);

        await _files.Received(1).DeleteFileAsync(firstFile, state.ApplicationId!.Value, Arg.Any<CancellationToken>());
        await _files.Received(1).DeleteFileAsync(secondFile, state.ApplicationId!.Value, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStillRemoveItem_WhenFileDeletionFails()
    {
        var fileId = Guid.NewGuid();
        _files.DeleteFileAsync(fileId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("already deleted"));
        SetupCollection(ItemWithFiles("i1", UploadsJson(fileId)));

        var result = await _service.ExecuteAsync(State(), FieldId, "i1", FlowId, confirmed: true);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Empty(RemainingItems());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotDeleteFiles_WhenNoFieldHoldsAFileArray()
    {
        var item = new Dictionary<string, object?>
        {
            ["id"] = "i1",
            ["name"] = "Ada",
            ["nothing"] = null,
            ["blank"] = string.Empty,
            ["empty"] = "[]"
        };
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object>
        {
            [FieldId] = JsonSerializer.Serialize(new[] { item })
        });

        await _service.ExecuteAsync(State(), FieldId, "i1", FlowId, confirmed: true);

        await _files.DidNotReceive().DeleteFileAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIgnoreMalformedFileJson()
    {
        SetupCollection(new Dictionary<string, object>
        {
            ["id"] = "i1",
            ["evidence"] = "[ not json"
        });

        var result = await _service.ExecuteAsync(State(), FieldId, "i1", FlowId, confirmed: true);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        await _files.DidNotReceive().DeleteFileAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotDeleteFiles_WhenApplicationIdIsMissing()
    {
        var state = State();
        state.ApplicationId = null;
        SetupCollection(ItemWithFiles("i1", UploadsJson(Guid.NewGuid())));

        await _service.ExecuteAsync(state, FieldId, "i1", FlowId, confirmed: true);

        await _files.DidNotReceive().DeleteFileAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static FormEngineWorkState State() =>
        new()
        {
            ReferenceNumber = "REF-1",
            TaskId = "t1",
            ApplicationId = Guid.NewGuid(),
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

    private static MultiCollectionFlowConfiguration Flow() =>
        new()
        {
            FlowId = FlowId,
            FieldId = FieldId,
            Title = "Members",
            Pages = []
        };

    private (TaskGroup Group, TaskModel Task) RegisterTask(params MultiCollectionFlowConfiguration[] flows)
    {
        var task = new TaskModel
        {
            TaskId = "t1",
            TaskName = "Members",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [],
            Summary = new TaskSummaryConfiguration { Mode = "multiCollectionFlow", Flows = [.. flows] }
        };
        var group = new TaskGroup
        {
            GroupId = "g1",
            GroupName = "g",
            GroupOrder = 1,
            GroupStatus = "NotStarted",
            Tasks = [task]
        };
        _templates.FindTask(Arg.Any<FormTemplate>(), Arg.Any<string>()).Returns((group, task));

        return (group, task);
    }

    private static Dictionary<string, object> Item(string id, string name) =>
        new() { ["id"] = id, ["name"] = name };

    private static Dictionary<string, object> ItemWithFiles(string id, string uploadsJson) =>
        new() { ["id"] = id, ["name"] = "Ada", ["evidence"] = uploadsJson };

    private static string UploadsJson(params Guid[] fileIds) =>
        JsonSerializer.Serialize(fileIds
            .Select(id => new UploadDto { Id = id, OriginalFileName = $"{id}.pdf" })
            .ToList());

    private void SetupCollection(params Dictionary<string, object>[] items) =>
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object>
        {
            [FieldId] = JsonSerializer.Serialize(items)
        });

    private List<Dictionary<string, object>> RemainingItems()
    {
        var accumulated = (Dictionary<string, object>)_responses.ReceivedCalls()
            .Last(c => c.GetMethodInfo().Name == nameof(IApplicationResponseService.AccumulateFormData))
            .GetArguments()[0]!;

        return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
            accumulated[FieldId].ToString()!)!;
    }
}
