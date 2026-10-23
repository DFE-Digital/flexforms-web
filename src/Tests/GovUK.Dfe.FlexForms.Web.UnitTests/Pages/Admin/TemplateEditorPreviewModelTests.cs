using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Web.Pages.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Admin;

public class TemplateEditorPreviewModelTests
{
    private const string Schema = """
        {
          "templateId": "tpl", "templateName": "T", "description": "",
          "taskGroups": [ { "groupId": "g", "groupName": "G", "groupOrder": 1, "groupStatus": "NotStarted", "tasks": [
            { "taskId": "linear", "taskName": "Linear", "taskOrder": 1, "taskStatus": "NotStarted", "pages": [
              { "pageId": "p1", "slug": "p1", "title": "One", "description": "", "pageOrder": 1, "fields": [] } ] },
            { "taskId": "lists", "taskName": "Lists", "taskOrder": 2, "taskStatus": "NotStarted", "pages": null,
              "summary": { "mode": "multiCollectionFlow", "flows": [
                { "flowId": "trusts", "title": "Trusts", "fieldId": "trusts", "pages": [
                  { "pageId": "trust-page", "slug": "t", "title": "Trust", "description": "", "pageOrder": 1, "fields": [] } ] } ] } } ] } ],
          "conditionalLogic": [
            { "id": "on", "enabled": true, "conditionGroup": { "logicalOperator": "AND", "conditions": [] },
              "affectedElements": [ { "elementId": "p1", "elementType": "page", "action": "show" } ] },
            { "id": "off", "enabled": false, "conditionGroup": { "logicalOperator": "AND", "conditions": [] },
              "affectedElements": [ { "elementId": "trust-page", "elementType": "page", "action": "show" } ] } ]
        }
        """;

    private readonly ITemplateValidationService _validation = Substitute.For<ITemplateValidationService>();
    private readonly TemplateEditorPreviewModel _model;

    public TemplateEditorPreviewModelTests()
    {
        _validation.ValidateTemplateJson(Arg.Any<string>()).Returns((true, new List<string>()));
        _model = new TemplateEditorPreviewModel(_validation);
    }

    [Fact]
    public void OnGet_ShouldSendPeopleBackToTheEditor()
    {
        Assert.Equal("/Admin/TemplateEditor", Assert.IsType<RedirectToPageResult>(_model.OnGet()).PageName);
    }

    [Fact]
    public void OnPost_ShouldExplainWhyInvalidJsonCantBePreviewed()
    {
        _model.Schema = "{ not json";

        Assert.IsType<PageResult>(_model.OnPost());

        Assert.StartsWith("This template can't be previewed", _model.ParseError);
        Assert.Null(_model.Task);
    }

    [Fact]
    public void OnPost_ShouldShowTheRequestedTaskWithItsCollectionPages_AndOnlyEnabledRuleTargets()
    {
        _model.Schema = Schema;
        _model.TaskId = "lists";

        _model.OnPost();

        Assert.Null(_model.ParseError);
        Assert.Equal("lists", _model.Task!.TaskId);
        var set = Assert.Single(_model.PageSets);
        Assert.Equal("Repeatable list: Trusts", set.Title);
        Assert.Equal("trust-page", Assert.Single(set.Pages).PageId);
        Assert.Equal(["p1"], _model.RuleTargets);
    }

    [Fact]
    public void OnPost_ShouldFallBackToTheFirstTask_AndPassOnSaveProblems()
    {
        _validation.ValidateTemplateJson(Schema).Returns((false, new List<string> { "a problem" }));
        _model.Schema = Schema;
        _model.TaskId = "missing";

        _model.OnPost();

        Assert.Equal("linear", _model.TaskId);
        Assert.Null(Assert.Single(_model.PageSets).Title);
        Assert.Equal(["a problem"], _model.Problems);
    }
}
