using System.Text.Json;
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

public class SaveFormPageServiceCoverageTests
{
    private readonly ITemplateManagementService _templates = Substitute.For<ITemplateManagementService>();
    private readonly IFormValidationOrchestrator _validation = Substitute.For<IFormValidationOrchestrator>();
    private readonly IFormNavigationService _navigation = Substitute.For<IFormNavigationService>();
    private readonly IConditionalLogicOrchestrator _conditionalLogic = Substitute.For<IConditionalLogicOrchestrator>();
    private readonly IApplicationResponseService _responses = Substitute.For<IApplicationResponseService>();
    private readonly ICollectionFlowProgressStore _flowProgress = Substitute.For<ICollectionFlowProgressStore>();
    private readonly IFormSessionStore _session = Substitute.For<IFormSessionStore>();
    private readonly INavigationHistoryService _history = Substitute.For<INavigationHistoryService>();
    private readonly IFormStateManager _formState = Substitute.For<IFormStateManager>();
    private readonly IComplexFieldConfigurationService _complexFields = Substitute.For<IComplexFieldConfigurationService>();
    private readonly IDerivedCollectionFlowService _derivedFlows = Substitute.For<IDerivedCollectionFlowService>();
    private readonly IApplicationStateService _applicationState = Substitute.For<IApplicationStateService>();
    private readonly SaveFormPageService _service;

    public SaveFormPageServiceCoverageTests()
    {
        _validation.ValidatePage(default!, default!, default).ReturnsForAnyArgs(FormValidationResult.Success);
        _conditionalLogic.ApplyConditionalLogicAsync(default!, default!, default)
            .ReturnsForAnyArgs(new FormConditionalState());
        _conditionalLogic.GetNextPageAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs((string?)null);
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object>());
        _flowProgress.Load(Arg.Any<string>(), Arg.Any<string>()).Returns(new Dictionary<string, object>());
        _navigation.GetTaskSummaryUrl(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => $"/applications/{call.ArgAt<string>(1)}/{call.ArgAt<string>(0)}");
        _navigation.GetCollectionFlowSummaryUrl(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => $"/applications/{call.ArgAt<string>(1)}/{call.ArgAt<string>(0)}/summary");
        _navigation.GetSubFlowPageUrl(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => $"/applications/{call.ArgAt<string>(1)}/{call.ArgAt<string>(0)}/flow/{call.ArgAt<string>(2)}/{call.ArgAt<string>(3)}/{call.ArgAt<string>(4)}");
        _navigation.GetTaskListUrl(Arg.Any<string>())
            .Returns(call => $"/applications/{call.ArgAt<string>(0)}");
        _formState.ShouldShowCollectionFlowSummary(Arg.Any<TaskModel>()).Returns(false);
        _formState.ShouldShowDerivedCollectionFlowSummary(Arg.Any<TaskModel>()).Returns(false);
        _applicationState.CalculateTaskStatus(default!, default!, default!, default, default!)
            .ReturnsForAnyArgs(Domain.Models.TaskStatus.InProgress);

        _service = new SaveFormPageService(
            _templates,
            new PostedFormDataBinder(),
            Substitute.For<IFormFileFieldService>(),
            _validation,
            _responses,
            _flowProgress,
            _session,
            _history,
            _navigation,
            _formState,
            _conditionalLogic,
            _complexFields,
            _derivedFlows,
            _applicationState,
            NullLogger<SaveFormPageService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectToTaskSummary_WhenLastPageSaved()
    {
        var page = CreatePage("p1", returnToSummaryPage: false);
        var task = CreateStandardTask(page);
        Register(task, page);

        var state = EditablePageState("p1", task.TaskId);
        var result = await _service.ExecuteAsync(state, EmptyPostedFields(), null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}", result.RedirectUrl);
        _history.Received().Clear($"REF-1:{task.TaskId}");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectToTaskSummary_WhenReturnToSummaryPageIsTrue()
    {
        var first = CreatePage("p1", returnToSummaryPage: true);
        var second = CreatePage("p2", returnToSummaryPage: false);
        var task = CreateStandardTask(first, second);
        Register(task, first);

        var state = EditablePageState("p1", task.TaskId);
        var result = await _service.ExecuteAsync(state, EmptyPostedFields(), null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}", result.RedirectUrl);
        _history.Received().Clear($"REF-1:{task.TaskId}");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectToNextSubFlowPage_WhenCollectionFlowNotLast()
    {
        var fp1 = CreatePage("fp1", returnToSummaryPage: false);
        var fp2 = CreatePage("fp2", returnToSummaryPage: false);
        var task = CreateCollectionFlowTask("f1", "members", fp1, fp2);
        Register(task, fp1);

        var state = EditablePageState("flow/f1/i1/fp1", task.TaskId);
        var result = await _service.ExecuteAsync(state, Posted("name", "Ada"), null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}/flow/f1/i1/fp2", result.RedirectUrl);
        _flowProgress.Received().Save("f1", "i1", Arg.Any<Dictionary<string, object>>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectToCollectionSummary_WhenCollectionFlowLastPageSaved()
    {
        var fp1 = CreatePage("fp1", returnToSummaryPage: false);
        var fp2 = CreatePage("fp2", returnToSummaryPage: false);
        var task = CreateCollectionFlowTask("f1", "members", fp1, fp2);
        Register(task, fp2);
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object>
        {
            ["members"] = """[{"id":"i1","name":"Ada"}]"""
        });

        var state = EditablePageState("flow/f1/i1/fp2", task.TaskId);
        var result = await _service.ExecuteAsync(state, Posted("name", "Ada Lovelace"), null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}/summary", result.RedirectUrl);
        Assert.Contains("updated", result.SuccessMessage!, StringComparison.OrdinalIgnoreCase);
        _flowProgress.Received().Clear("f1", "i1");
        _history.Received().Clear($"REF-1:{task.TaskId}:flow:f1:i1");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectWithPersistErrors_WhenCollectionFlowValidationFails()
    {
        var fp1 = CreatePage("fp1", returnToSummaryPage: false);
        var task = CreateCollectionFlowTask("f1", "members", fp1);
        Register(task, fp1);
        _validation.ValidatePage(default!, default!, default)
            .ReturnsForAnyArgs(new FormValidationResult([new FormValidationError("name", "Enter a name")]));

        var state = EditablePageState("flow/f1/i1/fp1", task.TaskId);
        var result = await _service.ExecuteAsync(state, EmptyPostedFields(), null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}/flow/f1/i1/fp1", result.RedirectUrl);
        Assert.True(result.PersistErrors);
        Assert.Contains(result.Errors, e => e.Message == "Enter a name");
        _flowProgress.Received().Save("f1", "i1", Arg.Any<Dictionary<string, object>>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMergeAutocompleteMultiSelect_WhenNewValuePosted()
    {
        const string fieldId = "tags";
        var page = CreatePage("p1", returnToSummaryPage: false, fields:
        [
            new Field
            {
                FieldId = fieldId,
                Type = "complexField",
                Label = new Label { Value = "Tags" },
                Order = 1,
                ComplexField = new ComplexField { Id = "cf-tags" }
            }
        ]);
        var task = CreateStandardTask(page);
        Register(task, page);

        _complexFields.GetConfiguration("cf-tags").Returns(new ComplexFieldConfiguration
        {
            Id = "cf-tags",
            FieldType = "autocomplete",
            AllowMultiple = true
        });
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object>
        {
            [fieldId] = """["Alpha"]"""
        });

        var state = EditablePageState("p1", task.TaskId);
        state.Data[fieldId] = """{"label":"Beta","value":"beta"}""";

        Dictionary<string, object>? accumulated = null;
        _responses.When(x => x.AccumulateFormData(Arg.Any<Dictionary<string, object>>()))
            .Do(call => accumulated = call.Arg<Dictionary<string, object>>());

        await _service.ExecuteAsync(state, Posted(fieldId, """{"label":"Beta","value":"beta"}"""), null);

        Assert.NotNull(accumulated);
        var merged = JsonSerializer.Deserialize<List<JsonElement>>(accumulated![fieldId].ToString()!)!;
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectToCollectionFlowSummary_WhenTaskSummaryPostOnMultiCollectionTask()
    {
        var task = CreateCollectionFlowTask("f1", "members", CreatePage("fp1", returnToSummaryPage: false));
        Register(task);
        _formState.ShouldShowCollectionFlowSummary(task).Returns(true);

        var state = EditablePageState("", task.TaskId);
        state.CurrentPageId = string.Empty;
        var result = await _service.ExecuteAsync(state, EmptyPostedFields(), null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}/summary", result.RedirectUrl);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectToPage_WhenDerivedSummaryCompletedFromPage()
    {
        var page = CreatePage("p1", returnToSummaryPage: false);
        var task = CreateDerivedFlowTask("df1", "sources", page);
        Register(task, page);
        _formState.ShouldShowDerivedCollectionFlowSummary(task).Returns(true);

        var state = EditablePageState("p1", task.TaskId);
        var result = await _service.ExecuteAsync(state, EmptyPostedFields(), "true");

        Assert.Equal(FormEngineOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal("/FormEngine/RenderForm", result.PageName);
        await _applicationState.Received().SaveTaskStatusAsync(
            state.ApplicationId!.Value,
            task.TaskId,
            Domain.Models.TaskStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStayWithErrors_WhenDerivedSummaryCompleteButItemsUnsigned()
    {
        var task = CreateDerivedFlowTask("df1", "sources");
        Register(task);
        _formState.ShouldShowDerivedCollectionFlowSummary(task).Returns(true);
        _derivedFlows.GenerateItemsFromSourceField("sources", Arg.Any<Dictionary<string, object>>(), Arg.Any<DerivedCollectionFlowConfiguration>())
            .Returns([new DerivedCollectionItem { Id = "item1", DisplayName = "Item 1" }]);
        _derivedFlows.GetItemStatuses("declarations", Arg.Any<Dictionary<string, object>>())
            .Returns(new Dictionary<string, string>());

        var state = EditablePageState("", task.TaskId);
        state.CurrentPageId = string.Empty;
        var result = await _service.ExecuteAsync(state, EmptyPostedFields(), "true");

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(FormState.DerivedCollectionFlowSummary, result.FormState);
        Assert.Contains(result.Errors, e => e.Message.Contains("sign the declaration", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectToTaskList_WhenDerivedSummarySavedWithoutCompletion()
    {
        var task = CreateDerivedFlowTask("df1", "sources");
        Register(task);
        _formState.ShouldShowDerivedCollectionFlowSummary(task).Returns(true);
        _applicationState.CalculateTaskStatus(default!, default!, default!, default, default!)
            .ReturnsForAnyArgs(Domain.Models.TaskStatus.NotStarted);

        var state = EditablePageState("", task.TaskId);
        state.CurrentPageId = string.Empty;
        var result = await _service.ExecuteAsync(state, EmptyPostedFields(), null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal("/applications/REF-1", result.RedirectUrl);
        await _applicationState.Received().SaveTaskStatusAsync(
            state.ApplicationId!.Value,
            task.TaskId,
            Domain.Models.TaskStatus.NotStarted);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStayWithErrors_WhenStandardPageValidationFails()
    {
        var page = CreatePage("p1", returnToSummaryPage: false);
        var task = CreateStandardTask(page);
        Register(task, page);
        _validation.ValidatePage(default!, default!, default)
            .ReturnsForAnyArgs(new FormValidationResult([new FormValidationError("name", "Enter a name")]));

        var result = await _service.ExecuteAsync(EditablePageState("p1", task.TaskId), EmptyPostedFields(), null);

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.True(result.PersistErrors);
        Assert.Contains(result.Errors, e => e.Message == "Enter a name");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectToApplications_WhenTaskCannotBeResolved()
    {
        _templates.FindPage(Arg.Any<FormTemplate>(), Arg.Any<string>())
            .Returns(((TaskGroup?)null, (TaskModel?)null, (PageModel?)null));

        var result = await _service.ExecuteAsync(EditablePageState("missing", "unknown"), EmptyPostedFields(), null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal("/applications/REF-1", result.RedirectUrl);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectToNextStandardPage_WhenSequentialPageExists()
    {
        var first = CreatePage("p1", returnToSummaryPage: false);
        var second = CreatePage("p2", returnToSummaryPage: false);
        var task = CreateStandardTask(first, second);
        Register(task, first);

        var result = await _service.ExecuteAsync(EditablePageState("p1", task.TaskId), Posted("name", "Ada"), null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}/p2", result.RedirectUrl);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectToConditionalNextPage_WhenOrchestratorReturnsPage()
    {
        var first = CreatePage("p1", returnToSummaryPage: false);
        var task = CreateStandardTask(first, CreatePage("p2", returnToSummaryPage: false));
        Register(task, first);
        _conditionalLogic.GetNextPageAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs("p3");

        var result = await _service.ExecuteAsync(EditablePageState("p1", task.TaskId), Posted("name", "Ada"), null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}/p3", result.RedirectUrl);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFollowConditionalNext_WhenReturnToSummaryHasShowPageTrigger()
    {
        var first = CreatePage("p1", returnToSummaryPage: true);
        var task = CreateStandardTask(first, CreatePage("p2", returnToSummaryPage: false));
        Register(task, first);
        _conditionalLogic.GetNextPageAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs("p2");

        var state = EditablePageState("p1", task.TaskId);
        state.Template!.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Enabled = true,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = "AND",
                    Conditions = [new Condition { TriggerField = "name", Operator = "equals", Value = "Ada" }]
                },
                AffectedElements = [new AffectedElement { ElementId = "p2", ElementType = "page", Action = "show" }]
            }
        ];

        state.Data["name"] = "Ada";
        var result = await _service.ExecuteAsync(state, Posted("Data[name]", "Ada"), null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}/p2", result.RedirectUrl);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSkipHiddenCollectionFlowPages()
    {
        var fp1 = CreatePage("fp1", returnToSummaryPage: false);
        var fp2 = CreatePage("fp2", returnToSummaryPage: false);
        var fp3 = CreatePage("fp3", returnToSummaryPage: false);
        var task = CreateCollectionFlowTask("f1", "members", fp1, fp2, fp3);
        Register(task, fp1);
        _conditionalLogic.ApplyConditionalLogicAsync(default!, default!, default)
            .ReturnsForAnyArgs(new FormConditionalState { PageVisibility = { ["fp2"] = false } });

        var result = await _service.ExecuteAsync(
            EditablePageState("flow/f1/i1/fp1", task.TaskId),
            Posted("name", "Ada"),
            null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}/flow/f1/i1/fp3", result.RedirectUrl);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateNewCollectionItem_WhenInstanceIsNotInSession()
    {
        var fp1 = CreatePage("fp1", returnToSummaryPage: false, fields:
        [
            new Field { FieldId = "name", Type = "text", Label = new Label { Value = "Name" }, Order = 1 }
        ]);
        var task = CreateCollectionFlowTask("f1", "members", fp1);
        Register(task, fp1);

        _flowProgress.Load("f1", "new-item").Returns(new Dictionary<string, object> { ["name"] = "Ada" });

        Dictionary<string, object>? accumulated = null;
        _responses.When(x => x.AccumulateFormData(Arg.Any<Dictionary<string, object>>()))
            .Do(call => accumulated = call.Arg<Dictionary<string, object>>());

        var result = await _service.ExecuteAsync(
            EditablePageState("flow/f1/new-item/fp1", task.TaskId),
            Posted("Data[name]", "Ada"),
            null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}/summary", result.RedirectUrl);
        Assert.NotNull(accumulated);
        Assert.Contains("new-item", accumulated!["members"].ToString()!);
        Assert.Contains("Ada", accumulated["members"].ToString()!);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldKeepExistingUploadJson_WhenPlaceholderPostedOnCollectionItem()
    {
        var fp1 = CreatePage("fp1", returnToSummaryPage: false, fields:
        [
            new Field { FieldId = "file", Type = "upload", Label = new Label { Value = "File" }, Order = 1 }
        ]);
        var task = CreateCollectionFlowTask("f1", "members", fp1);
        Register(task, fp1);
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object>
        {
            ["members"] = """[{"id":"i1","file":"[{\"id\":\"file-1\"}]"}]"""
        });
        _flowProgress.Load("f1", "i1").Returns(new Dictionary<string, object>
        {
            ["file"] = FormEngineConstants.UploadFieldSessionPlaceholder
        });

        Dictionary<string, object>? accumulated = null;
        _responses.When(x => x.AccumulateFormData(Arg.Any<Dictionary<string, object>>()))
            .Do(call => accumulated = call.Arg<Dictionary<string, object>>());

        var state = EditablePageState("flow/f1/i1/fp1", task.TaskId);
        state.Data["file"] = FormEngineConstants.UploadFieldSessionPlaceholder;
        var result = await _service.ExecuteAsync(state, Posted("file", FormEngineConstants.UploadFieldSessionPlaceholder), null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.NotNull(accumulated);
        Assert.Contains("file-1", accumulated!["members"].ToString()!);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSaveDerivedFlowDeclaration_WhenPageIsValid()
    {
        var page = CreatePage("p1", returnToSummaryPage: false);
        var task = CreateDerivedFlowTask("df1", "sources", page);
        Register(task, page);
        _derivedFlows.When(x => x.SaveItemDeclaration(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, object>>()))
            .Do(call =>
            {
                var formData = call.ArgAt<Dictionary<string, object>>(4);
                formData["declarations_status_item1"] = "Signed";
                formData["declarations_data_item1"] = "{}";
            });
        _derivedFlows.GenerateItemsFromSourceField("sources", Arg.Any<Dictionary<string, object>>(), Arg.Any<DerivedCollectionFlowConfiguration>())
            .Returns([new DerivedCollectionItem { Id = "item1", DisplayName = "Trust A" }]);

        var result = await _service.ExecuteAsync(
            EditablePageState("df1/derived/item1/p1", task.TaskId, task),
            Posted("Data[chair]", "Jane"),
            null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}", result.RedirectUrl);
        Assert.Contains("Trust A", result.SuccessMessage!);
        _derivedFlows.Received().SaveItemDeclaration(
            "declarations",
            "item1",
            Arg.Any<Dictionary<string, object>>(),
            "Signed",
            Arg.Any<Dictionary<string, object>>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPersistDerivedFlowErrors_WhenDeclarationPageIsInvalid()
    {
        var page = CreatePage("p1", returnToSummaryPage: false);
        var task = CreateDerivedFlowTask("df1", "sources", page);
        Register(task, page);
        _validation.ValidatePage(default!, default!, default)
            .ReturnsForAnyArgs(new FormValidationResult([new FormValidationError("chair", "Sign the declaration")]));

        var result = await _service.ExecuteAsync(
            EditablePageState("df1/derived/item1/p1", task.TaskId, task),
            EmptyPostedFields(),
            null);

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal($"/applications/REF-1/{task.TaskId}/df1/derived/item1/p1", result.RedirectUrl);
        Assert.True(result.PersistErrors);
        Assert.Contains(result.Errors, e => e.Message == "Sign the declaration");
        _derivedFlows.DidNotReceive().SaveItemDeclaration(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStay_WhenDerivedSummaryHasNoItemsAndCompletionRequested()
    {
        var task = CreateDerivedFlowTask("df1", "sources");
        task.Summary!.DerivedFlows![0].NoItemsErrorMessage = "Add an organisation first";
        Register(task);
        _formState.ShouldShowDerivedCollectionFlowSummary(task).Returns(true);
        _derivedFlows.GenerateItemsFromSourceField("sources", Arg.Any<Dictionary<string, object>>(), Arg.Any<DerivedCollectionFlowConfiguration>())
            .Returns([]);

        var state = EditablePageState("", task.TaskId);
        state.CurrentPageId = string.Empty;
        var result = await _service.ExecuteAsync(state, EmptyPostedFields(), "true");

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message == "Add an organisation first");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCompleteDerivedSummary_WhenAllItemsAreSigned()
    {
        var task = CreateDerivedFlowTask("df1", "sources");
        Register(task);
        _formState.ShouldShowDerivedCollectionFlowSummary(task).Returns(true);
        _derivedFlows.GenerateItemsFromSourceField("sources", Arg.Any<Dictionary<string, object>>(), Arg.Any<DerivedCollectionFlowConfiguration>())
            .Returns([new DerivedCollectionItem { Id = "item1", DisplayName = "Trust A" }]);
        _derivedFlows.GetItemStatuses("declarations", Arg.Any<Dictionary<string, object>>())
            .Returns(new Dictionary<string, string> { ["item1"] = "Signed" });

        var state = EditablePageState("", task.TaskId);
        state.CurrentPageId = string.Empty;
        var result = await _service.ExecuteAsync(state, EmptyPostedFields(), "on");

        Assert.Equal(FormEngineOutcomeKind.Redirect, result.Kind);
        Assert.Equal("/applications/REF-1", result.RedirectUrl);
        await _applicationState.Received().SaveTaskStatusAsync(
            state.ApplicationId!.Value,
            task.TaskId,
            Domain.Models.TaskStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMarkDerivedPageIncomplete_WhenCompletionNotRequested()
    {
        var page = CreatePage("p1", returnToSummaryPage: false);
        var task = CreateDerivedFlowTask("df1", "sources", page);
        Register(task, page);
        _formState.ShouldShowDerivedCollectionFlowSummary(task).Returns(true);
        _applicationState.CalculateTaskStatus(default!, default!, default!, default, default!)
            .ReturnsForAnyArgs(Domain.Models.TaskStatus.InProgress);

        var result = await _service.ExecuteAsync(EditablePageState("p1", task.TaskId), EmptyPostedFields(), null);

        Assert.Equal(FormEngineOutcomeKind.RedirectToPage, result.Kind);
        await _applicationState.Received().SaveTaskStatusAsync(
            Arg.Any<Guid>(),
            task.TaskId,
            Domain.Models.TaskStatus.InProgress);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIgnoreDuplicateAutocompleteValue()
    {
        const string fieldId = "tags";
        var page = CreatePage("p1", returnToSummaryPage: false, fields:
        [
            new Field
            {
                FieldId = fieldId,
                Type = "complexField",
                Label = new Label { Value = "Tags" },
                Order = 1,
                ComplexField = new ComplexField { Id = "cf-tags" }
            }
        ]);
        var task = CreateStandardTask(page);
        Register(task, page);
        _complexFields.GetConfiguration("cf-tags").Returns(new ComplexFieldConfiguration
        {
            Id = "cf-tags",
            FieldType = "autocomplete",
            AllowMultiple = true
        });
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object>
        {
            [fieldId] = """["Beta"]"""
        });

        Dictionary<string, object>? accumulated = null;
        _responses.When(x => x.AccumulateFormData(Arg.Any<Dictionary<string, object>>()))
            .Do(call => accumulated = call.Arg<Dictionary<string, object>>());

        var state = EditablePageState("p1", task.TaskId);
        state.Data[fieldId] = "Beta";
        await _service.ExecuteAsync(state, Posted("Data[tags]", "Beta"), null);

        Assert.NotNull(accumulated);
        var merged = JsonSerializer.Deserialize<List<JsonElement>>(accumulated![fieldId].ToString()!)!;
        Assert.Single(merged);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStayWithNoWritePermission_WhenNotEditable()
    {
        var page = CreatePage("p1", returnToSummaryPage: false);
        var task = CreateStandardTask(page);
        Register(task, page);
        var state = EditablePageState("p1", task.TaskId);
        state.IsEditable = false;

        var result = await _service.ExecuteAsync(state, EmptyPostedFields(), null);

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message == FormEngineMessages.NoWritePermission);
        await _responses.DidNotReceive().SaveApplicationResponseAsync(
            Arg.Any<Guid>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    private void Register(TaskModel task, PageModel? page = null)
    {
        var group = new TaskGroup { GroupId = "g1", GroupName = "g", GroupOrder = 1, GroupStatus = "NotStarted", Tasks = [task] };
        _templates.FindTask(Arg.Any<FormTemplate>(), Arg.Any<string>()).Returns((group, task));
        if (page != null)
            _templates.FindPage(Arg.Any<FormTemplate>(), Arg.Any<string>()).Returns((group, task, page));
    }

    private static FormEngineWorkState EditablePageState(string pageId, string taskId, TaskModel? task = null) =>
        new()
        {
            ReferenceNumber = "REF-1",
            ApplicationId = Guid.NewGuid(),
            TaskId = taskId,
            CurrentPageId = pageId,
            IsEditable = true,
            Template = new FormTemplate
            {
                TemplateId = "tpl",
                TemplateName = "tpl",
                Description = "tpl",
                TaskGroups = task == null
                    ? []
                    :
                    [
                        new TaskGroup
                        {
                            GroupId = "g1",
                            GroupName = "g",
                            GroupOrder = 1,
                            GroupStatus = "NotStarted",
                            Tasks = [task]
                        }
                    ]
            },
            FormData = new Dictionary<string, object>(),
            Data = new Dictionary<string, object>()
        };

    private static PageModel CreatePage(string pageId, bool returnToSummaryPage, List<Field>? fields = null) =>
        new()
        {
            PageId = pageId,
            Slug = pageId,
            Title = pageId,
            Description = pageId,
            PageOrder = 1,
            ReturnToSummaryPage = returnToSummaryPage,
            Fields = fields ?? []
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

    private static TaskModel CreateDerivedFlowTask(string flowId, string sourceFieldId, params PageModel[] pages) =>
        new()
        {
            TaskId = "t1",
            TaskName = "Declarations",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [.. pages],
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
                        Title = "Declarations",
                        Pages = [.. pages]
                    }
                ]
            }
        };

    private static Dictionary<string, IReadOnlyList<string>> EmptyPostedFields() =>
        new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, IReadOnlyList<string>> Posted(string key, string value) =>
        new(StringComparer.OrdinalIgnoreCase) { [key] = [value] };
}
