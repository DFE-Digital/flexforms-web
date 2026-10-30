using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using GovUK.Dfe.FlexForms.Web.Pages.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Admin;

public class TemplateStudioPreviewModelTests
{
    private const string Schema = """
        {
          "templateId": "tpl", "templateName": "T", "description": "",
          "taskGroups": [ { "groupId": "g", "groupName": "G", "groupOrder": 1, "groupStatus": "NotStarted", "tasks": [
            { "taskId": "linear", "taskName": "Linear", "taskOrder": 1, "taskStatus": "NotStarted", "pages": [
              { "pageId": "p1", "slug": "p1", "title": "One", "description": "", "pageOrder": 1, "returnToSummaryPage": false, "fields": [
                { "fieldId": "name", "type": "text", "order": 1, "label": { "value": "Name" },
                  "validations": [ { "type": "required", "rule": true, "message": "Enter a name" } ] } ] },
              { "pageId": "p2", "slug": "p2", "title": "Two", "description": "", "pageOrder": 2, "fields": [
                { "fieldId": "trust", "type": "complexField", "order": 1, "label": { "value": "Trust" }, "complexField": { "id": "TrustSearch" } } ] } ] },
            { "taskId": "lists", "taskName": "Lists", "taskOrder": 2, "taskStatus": "NotStarted", "pages": null,
              "summary": { "mode": "multiCollectionFlow", "flows": [
                { "flowId": "trusts", "title": "Trusts", "fieldId": "trusts", "itemTitleBinding": "trustName",
                  "addItemMessage": "Added {trustName}", "deleteItemMessage": "Removed {trustName}", "pages": [
                  { "pageId": "trust-page", "slug": "t", "title": "Trust", "description": "", "pageOrder": 1, "fields": [
                    { "fieldId": "trustName", "type": "text", "order": 1, "label": { "value": "Trust name" } } ] } ] } ] } } ] } ],
          "conditionalLogic": [
            { "id": "on", "enabled": true, "conditionGroup": { "logicalOperator": "AND", "conditions": [] },
              "affectedElements": [ { "elementId": "p1", "elementType": "page", "action": "show" } ] },
            { "id": "off", "enabled": false, "conditionGroup": { "logicalOperator": "AND", "conditions": [] },
              "affectedElements": [ { "elementId": "trust-page", "elementType": "page", "action": "show" } ] } ]
        }
        """;

    private readonly ITemplateValidationService _validation = Substitute.For<ITemplateValidationService>();
    private readonly IComplexFieldConfigurationService _complexFields = Substitute.For<IComplexFieldConfigurationService>();
    private readonly InMemorySession _session = new();
    private readonly DefaultHttpContext _httpContext = new();

    public TemplateStudioPreviewModelTests()
    {
        _validation.ValidateTemplateJson(Arg.Any<string>()).Returns((true, new List<string>()));
        _complexFields.GetConfiguration("TrustSearch").Returns(new ComplexFieldConfiguration { Id = "TrustSearch", FieldType = "autocomplete" });
        _httpContext.Features.Set<ISessionFeature>(new StubSessionFeature { Session = _session });
    }

    private TemplateStudioPreviewModel NewModel(Dictionary<string, StringValues>? form = null)
    {
        var engine = new ConditionalLogicEngine(NullLogger<ConditionalLogicEngine>.Instance);
        var requirements = new FieldRequirementService(NullLogger<FieldRequirementService>.Instance);
        var derived = new DerivedCollectionFlowService(NullLogger<DerivedCollectionFlowService>.Instance);
        var preview = new DraftFormPreviewService(
            new PostedFormDataBinder(),
            new FormValidationOrchestrator(NullLogger<FormValidationOrchestrator>.Instance, engine, requirements),
            new ConditionalLogicOrchestrator(engine, NullLogger<ConditionalLogicOrchestrator>.Instance),
            requirements,
            derived,
            NullLogger<DraftFormPreviewService>.Instance);

        _httpContext.Request.Form = new FormCollection(form ?? []);
        return new TemplateStudioPreviewModel(_validation, preview, derived, _complexFields)
        {
            PageContext = new PageContext
            {
                HttpContext = _httpContext,
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            },
            TempData = new TempDataDictionary(_httpContext, Substitute.For<ITempDataProvider>())
        };
    }

    private void StartPreview(string? taskId = null)
    {
        var model = NewModel();
        model.Schema = Schema;
        model.TaskId = taskId;
        model.OnPost();
    }

    private TemplateStudioPreviewModel.PreviewState StoredState() =>
        JsonSerializer.Deserialize<TemplateStudioPreviewModel.PreviewState>(
            System.Text.Encoding.UTF8.GetString(_session.Values[TemplateStudioPreviewModel.SessionKey]))!;

    [Fact]
    public async Task OnGetAsync_ShouldSayThereIsNothingToPreview_WhenTheSessionHasNoPreview()
    {
        var model = NewModel();

        Assert.IsType<PageResult>(await model.OnGetAsync());

        Assert.Equal(TemplateStudioPreviewModel.PreviewView.NoPreview, model.View);
        Assert.Null(model.Template);
    }

    [Fact]
    public void OnPost_ShouldExplainWhyInvalidJsonCantBePreviewed()
    {
        var model = NewModel();
        model.Schema = "{ not json";

        Assert.IsType<PageResult>(model.OnPost());

        Assert.StartsWith("This template can't be previewed", model.ParseError);
        Assert.False(_session.Values.ContainsKey(TemplateStudioPreviewModel.SessionKey));
    }

    [Fact]
    public void OnPost_ShouldKeepTheTemplateForThePreview_AndOpenTheRequestedTask()
    {
        var model = NewModel();
        model.Schema = Schema;
        model.TaskId = "lists";

        var redirect = Assert.IsType<RedirectToPageResult>(model.OnPost());

        Assert.Equal("lists", redirect.RouteValues!["task"]);
        Assert.Equal(Schema, StoredState().Schema);
    }

    [Fact]
    public async Task OnGetAsync_ShouldShowTheTaskList_WhenNoTaskIsChosen()
    {
        StartPreview();
        var model = NewModel();

        await model.OnGetAsync();

        Assert.Equal(TemplateStudioPreviewModel.PreviewView.TaskList, model.View);
        Assert.Equal("tpl", model.Template!.TemplateId);
    }

    [Fact]
    public async Task OnGetAsync_ShouldListEveryPage_AndOnlyEnabledRuleTargets_InTheAllPagesView()
    {
        StartPreview();
        var model = NewModel();
        model.TaskId = "lists";
        model.ViewName = "all";

        await model.OnGetAsync();

        Assert.Equal(TemplateStudioPreviewModel.PreviewView.AllPages, model.View);
        var set = Assert.Single(model.PageSets);
        Assert.Equal("Repeatable list: Trusts", set.Title);
        Assert.Equal(["p1"], model.RuleTargets);
    }

    [Fact]
    public async Task OnPostAnswerAsync_ShouldShowTheTemplatesErrors_AndKeepThePage_WhenAnAnswerIsMissing()
    {
        StartPreview();
        var model = NewModel(new() { ["Data[name]"] = "" });
        model.TaskId = "linear";
        model.PageId = "p1";

        Assert.IsType<PageResult>(await model.OnPostAnswerAsync());

        Assert.Equal(TemplateStudioPreviewModel.PreviewView.Page, model.View);
        Assert.Equal("Enter a name", Assert.Single(model.Errors).Message);
        Assert.Empty(StoredState().Answers);
    }

    [Fact]
    public async Task OnPostAnswerAsync_ShouldKeepTheAnswer_AndGoToTheNextPage()
    {
        StartPreview();
        var model = NewModel(new() { ["Data[name]"] = "Ann" });
        model.TaskId = "linear";
        model.PageId = "p1";

        var redirect = Assert.IsType<RedirectToPageResult>(await model.OnPostAnswerAsync());

        Assert.Equal("p2", redirect.RouteValues!["pageId"]);
        Assert.Equal("Ann", StoredState().Answers["name"].ToString());
    }

    [Fact]
    public async Task OnPostAnswerAsync_ShouldStoreASampleSearchAnswer_AsTheSelectedResult()
    {
        StartPreview();
        var model = NewModel(new() { ["Data[trust]"] = "Oak Academy Trust" });
        model.TaskId = "linear";
        model.PageId = "p2";

        var redirect = Assert.IsType<RedirectToPageResult>(await model.OnPostAnswerAsync());

        Assert.False(redirect.RouteValues!.ContainsKey("pageId") && redirect.RouteValues["pageId"] is not null);
        using var stored = JsonDocument.Parse(StoredState().Answers["trust"].ToString()!);
        Assert.Equal("Oak Academy Trust", stored.RootElement.GetProperty("name").GetString());
        Assert.Equal("oak-academy-trust", stored.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task OnPostAnswerAsync_ShouldAddTheItemToTheRepeatableList_WithTheTemplatesMessage()
    {
        StartPreview();
        var model = NewModel(new() { ["Data[trustName]"] = "Elm Trust" });
        model.TaskId = "lists";
        model.PageId = "trust-page";
        model.ListId = "trusts";
        model.ItemId = "item-1";
        model.IsNewItem = true;

        var redirect = Assert.IsType<RedirectToPageResult>(await model.OnPostAnswerAsync());

        Assert.Equal("lists", redirect.RouteValues!["task"]);
        Assert.Equal("Added Elm Trust", model.Message);
        var items = FormEngineCollectionItems.Read(new Dictionary<string, object> { ["trusts"] = StoredState().Answers["trusts"].ToString()! }, "trusts");
        Assert.Equal("Elm Trust", Assert.Single(items)["trustName"].ToString());
    }

    [Fact]
    public async Task OnPostRemove_ShouldTakeTheItemOutOfTheList()
    {
        StartPreview();
        var add = NewModel(new() { ["Data[trustName]"] = "Elm Trust" });
        add.TaskId = "lists";
        add.PageId = "trust-page";
        add.ListId = "trusts";
        add.ItemId = "item-1";
        await add.OnPostAnswerAsync();

        var model = NewModel();
        model.TaskId = "lists";
        model.ListId = "trusts";
        model.ItemId = "item-1";
        model.OnPostRemove();

        Assert.Equal("Removed Elm Trust", model.Message);
        Assert.Equal("[]", StoredState().Answers["trusts"].ToString());
    }

    [Fact]
    public async Task OnPostCompleteAsync_ShouldListWhatsMissing_BeforeMarkingTheSectionComplete()
    {
        StartPreview();
        var model = NewModel();
        model.TaskId = "lists";
        model.IsTaskCompleted = true;

        Assert.IsType<PageResult>(await model.OnPostCompleteAsync());

        Assert.Equal(TemplateStudioPreviewModel.PreviewView.TaskSummary, model.View);
        Assert.Equal(["Add at least 1 item(s) to Trusts"], model.CompletionProblems);
        Assert.Empty(StoredState().Completed);
    }

    [Fact]
    public async Task OnPostCompleteAsync_ShouldMarkTheSectionComplete_WhenNothingIsMissing()
    {
        StartPreview();
        var answer = NewModel(new() { ["Data[name]"] = "Ann" });
        answer.TaskId = "linear";
        answer.PageId = "p1";
        await answer.OnPostAnswerAsync();

        var model = NewModel();
        model.TaskId = "linear";
        model.IsTaskCompleted = true;
        Assert.IsType<RedirectToPageResult>(await model.OnPostCompleteAsync());

        Assert.Contains("linear", StoredState().Completed);
        Assert.Equal("Linear is marked as complete", model.Message);
    }

    [Fact]
    public async Task OnGetAsync_ShouldShowThePageWithEarlierAnswers()
    {
        StartPreview();
        var answer = NewModel(new() { ["Data[name]"] = "Ann" });
        answer.TaskId = "linear";
        answer.PageId = "p1";
        await answer.OnPostAnswerAsync();

        var model = NewModel();
        model.TaskId = "linear";
        model.PageId = "p1";
        await model.OnGetAsync();

        Assert.Equal(TemplateStudioPreviewModel.PreviewView.Page, model.View);
        Assert.Equal("One", model.CurrentPage!.Title);
        Assert.Equal("Ann", model.Data["name"]);
    }

    [Fact]
    public async Task OnGetAsync_ShouldShowTheTaskSummary_WhenThePageIsNotInTheTask()
    {
        StartPreview();
        var model = NewModel();
        model.TaskId = "linear";
        model.PageId = "no-such-page";

        Assert.IsType<PageResult>(await model.OnGetAsync());

        Assert.Equal(TemplateStudioPreviewModel.PreviewView.TaskSummary, model.View);
    }

    [Fact]
    public void PageIdQueryName_ShouldNotBeTheRouteValueRazorPagesReserves()
    {
        var bind = typeof(TemplateStudioPreviewModel).GetProperty(nameof(TemplateStudioPreviewModel.PageId))!
            .GetCustomAttributes(typeof(BindPropertyAttribute), false).Cast<BindPropertyAttribute>().Single();

        Assert.NotEqual("page", bind.Name);
    }

    [Fact]
    public void OnPostRestart_ShouldClearTheAnswers_ButKeepTheTemplate()
    {
        StartPreview();
        var state = StoredState();
        state.Answers["name"] = "Ann";
        _session.Set(TemplateStudioPreviewModel.SessionKey, JsonSerializer.SerializeToUtf8Bytes(state));

        NewModel().OnPostRestart();

        Assert.Empty(StoredState().Answers);
        Assert.Equal(Schema, StoredState().Schema);
    }

    private sealed class StubSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new InMemorySession();
    }

    private sealed class InMemorySession : ISession
    {
        public Dictionary<string, byte[]> Values { get; } = [];
        public bool IsAvailable => true;
        public string Id => "test-session";
        public IEnumerable<string> Keys => Values.Keys;
        public void Clear() => Values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => Values.Remove(key);
        public void Set(string key, byte[] value) => Values[key] = value;
        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => Values.TryGetValue(key, out value);
    }
}
