using System.Text.Json;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

/// <summary>
/// Runs the preview against the real binder, rules engine and validation, so it behaves as the live form does.
/// </summary>
public class DraftFormPreviewServiceTests
{
    private const string TemplateJson = """
    {
      "templateId": "preview-test",
      "templateName": "Preview test",
      "description": "",
      "taskGroups": [{
        "groupId": "group", "groupName": "Group", "groupOrder": 1, "groupStatus": "NotStarted",
        "tasks": [{
          "taskId": "trust", "taskName": "Trust details", "taskOrder": 1, "taskStatus": "NotStarted",
          "pages": [
            { "pageId": "has-trust", "slug": "has-trust", "title": "Is there a trust?", "description": "", "pageOrder": 1, "returnToSummaryPage": false,
              "fields": [{ "fieldId": "hasTrust", "type": "radios", "order": 1, "required": true,
                "label": { "value": "Is there a trust?" },
                "options": [{ "value": "yes", "label": "Yes" }, { "value": "no", "label": "No" }],
                "validations": [{ "type": "required", "rule": true, "message": "Select yes if there is a trust" }] }] },
            { "pageId": "trust-name", "slug": "trust-name", "title": "Trust name", "description": "", "pageOrder": 2, "returnToSummaryPage": false,
              "fields": [{ "fieldId": "trustName", "type": "text", "order": 1,
                "label": { "value": "Trust name" },
                "validations": [{ "type": "required", "rule": true, "message": "Enter the trust name" }] }] },
            { "pageId": "opened", "slug": "opened", "title": "When did it open?", "description": "", "pageOrder": 3,
              "navigationAfterSave": "summary",
              "fields": [{ "fieldId": "openedOn", "type": "date", "order": 1, "label": { "value": "Opened on" } }] },
            { "pageId": "notes", "slug": "notes", "title": "Notes", "description": "", "pageOrder": 4,
              "fields": [{ "fieldId": "notes", "type": "text-area", "order": 1, "label": { "value": "Notes" } }] }
          ]
        }, {
          "taskId": "academies", "taskName": "Academies", "taskOrder": 2, "taskStatus": "NotStarted",
          "summary": { "mode": "multiCollectionFlow", "flows": [{
            "flowId": "academies-flow", "title": "Academies", "fieldId": "academies", "minItems": 1,
            "pages": [{ "pageId": "academy", "slug": "academy", "title": "Academy", "description": "", "pageOrder": 1,
              "fields": [{ "fieldId": "academyName", "type": "text", "order": 1, "label": { "value": "Academy name" } }] }]
          }] }
        }, {
          "taskId": "declarations", "taskName": "Declarations", "taskOrder": 3, "taskStatus": "NotStarted",
          "summary": { "mode": "derivedCollectionFlow", "derivedFlows": [{
            "flowId": "declaration-flow", "title": "Declarations", "sourceFieldId": "trustees", "sourceType": "checkboxes",
            "fieldId": "declarations", "unsignedItemErrorMessage": "Sign for {sourceName}",
            "pages": [{ "pageId": "declare", "slug": "declare", "title": "Declare", "description": "", "pageOrder": 1, "fields": [] }]
          }] }
        }]
      }],
      "conditionalLogic": [
        { "id": "trust-name-hide", "priority": 100, "enabled": true,
          "conditionGroup": { "logicalOperator": "AND", "conditions": [{ "triggerField": "hasTrust", "operator": "notEquals", "value": "yes", "dataType": "string" }] },
          "affectedElements": [{ "elementId": "trust-name", "elementType": "page", "action": "skip" }] },
        { "id": "trust-name-show", "priority": 101, "enabled": true,
          "conditionGroup": { "logicalOperator": "AND", "conditions": [{ "triggerField": "hasTrust", "operator": "equals", "value": "yes", "dataType": "string" }] },
          "affectedElements": [{ "elementId": "trust-name", "elementType": "page", "action": "show" }] }
      ]
    }
    """;

    private readonly FormTemplate _template = JsonSerializer.Deserialize<FormTemplate>(TemplateJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    private readonly DraftFormPreviewService _preview;

    public DraftFormPreviewServiceTests()
    {
        var engine = new ConditionalLogicEngine(NullLogger<ConditionalLogicEngine>.Instance);
        var requirements = new FieldRequirementService(NullLogger<FieldRequirementService>.Instance);
        _preview = new DraftFormPreviewService(
            new PostedFormDataBinder(),
            new FormValidationOrchestrator(NullLogger<FormValidationOrchestrator>.Instance, engine, requirements),
            new ConditionalLogicOrchestrator(engine, NullLogger<ConditionalLogicOrchestrator>.Instance),
            requirements,
            new DerivedCollectionFlowService(NullLogger<DerivedCollectionFlowService>.Instance),
            NullLogger<DraftFormPreviewService>.Instance);
    }

    private Domain.Models.Task TaskNamed(string id) => _template.TaskGroups[0].Tasks.Single(task => task.TaskId == id);

    private System.Threading.Tasks.Task<DraftPageOutcome> Submit(string pageId, Dictionary<string, object> data, params (string Key, string Value)[] posted)
    {
        var task = TaskNamed("trust");
        return _preview.SubmitPageAsync(
            _template, task, task.Pages!.Single(page => page.PageId == pageId), task.Pages!, isTaskPage: true, data,
            posted.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)[pair.Value]));
    }

    [Fact]
    public async Task SubmitPageAsync_ShouldShowTheTemplatesErrorMessage_WhenARequiredAnswerIsMissing()
    {
        var outcome = await Submit("has-trust", []);

        Assert.False(outcome.IsValid);
        var error = Assert.Single(outcome.Errors);
        Assert.Equal("hasTrust", error.FieldKey);
        Assert.Equal("Select yes if there is a trust", error.Message);
    }

    [Fact]
    public async Task SubmitPageAsync_ShouldGoToThePageARuleShows_WhenTheAnswerMatches()
    {
        var outcome = await Submit("has-trust", [], ("Data[hasTrust]", "yes"));

        Assert.True(outcome.IsValid);
        Assert.Equal("yes", outcome.Data["hasTrust"]);
        Assert.Equal("trust-name", outcome.NextPageId);
    }

    [Fact]
    public async Task SubmitPageAsync_ShouldSkipThePageARuleSkips_WhenTheAnswerDoesNotMatch()
    {
        var outcome = await Submit("has-trust", [], ("Data[hasTrust]", "no"));

        Assert.True(outcome.IsValid);
        Assert.Equal("opened", outcome.NextPageId);
    }

    [Fact]
    public async Task SubmitPageAsync_ShouldPutDatePartsTogether_AndFollowThePagesNavigationSetting()
    {
        var outcome = await Submit("opened", new Dictionary<string, object> { ["hasTrust"] = "no" },
            ("Data[openedOn].day", "3"), ("Data[openedOn].month", "9"), ("Data[openedOn].year", "2024"));

        Assert.True(outcome.IsValid);
        Assert.Equal("2024-09-03", outcome.Data["openedOn"]);
        Assert.Null(outcome.NextPageId);
    }

    [Fact]
    public async Task SubmitPageAsync_ShouldGoToTheSummary_AfterTheLastPage()
    {
        var outcome = await Submit("notes", new Dictionary<string, object> { ["hasTrust"] = "no" }, ("Data[notes]", "None"));

        Assert.Null(outcome.NextPageId);
        Assert.Equal("None", outcome.Data["notes"]);
    }

    [Fact]
    public async Task SubmitPageAsync_ShouldKeepEarlierAnswers()
    {
        var outcome = await Submit("trust-name", new Dictionary<string, object> { ["hasTrust"] = "yes" }, ("Data[trustName]", "Oak Trust"));

        Assert.Equal("yes", outcome.Data["hasTrust"]);
        Assert.Equal("Oak Trust", outcome.Data["trustName"]);
    }

    [Fact]
    public async Task CompletionProblemsAsync_ShouldListMissingRequiredAnswers_WithTheTemplatesMessages()
    {
        var problems = await _preview.CompletionProblemsAsync(_template, TaskNamed("trust"), new Dictionary<string, object> { ["hasTrust"] = "yes" });

        Assert.Equal(["Enter the trust name"], problems);
    }

    [Fact]
    public async Task SubmitPageAsync_ShouldGoBackToTheSummary_ByDefault()
    {
        var task = TaskNamed("trust");
        task.Pages![0].ReturnToSummaryPage = true;

        var outcome = await Submit("has-trust", [], ("Data[hasTrust]", "yes"));

        Assert.Null(outcome.NextPageId);
    }

    [Fact]
    public async Task CompletionProblemsAsync_ShouldAskForTheFewestItems_InARepeatableList()
    {
        var problems = await _preview.CompletionProblemsAsync(_template, TaskNamed("academies"), []);

        Assert.Equal(["Add at least 1 item(s) to Academies"], problems);
    }

    [Fact]
    public async Task CompletionProblemsAsync_ShouldNameEveryUnsignedItem_InRepeatedPages()
    {
        var problems = await _preview.CompletionProblemsAsync(_template, TaskNamed("declarations"),
            new Dictionary<string, object> { ["trustees"] = """["Ann","Bo"]""", ["declarations_status_ann"] = "Signed" });

        Assert.Equal(["Sign for Bo"], problems);
    }

    [Fact]
    public async Task CompletionProblemsAsync_ShouldSayThereAreNoItems_WhenTheSourceAnswerIsEmpty()
    {
        var problems = await _preview.CompletionProblemsAsync(_template, TaskNamed("declarations"), []);

        Assert.Equal(["You need to add at least one item before signing the Declarations"], problems);
    }
}
