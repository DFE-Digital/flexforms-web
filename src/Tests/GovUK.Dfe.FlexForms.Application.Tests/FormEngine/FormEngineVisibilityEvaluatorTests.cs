using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class FormEngineVisibilityEvaluatorTests
{
    [Fact]
    public void IsFieldHidden_ShouldReturnFalse_WhenNoConditionalLogic()
    {
        var evaluator = Evaluator(template: Dummy(), conditionalState: null);

        Assert.False(evaluator.IsFieldHidden("name"));
    }

    [Fact]
    public void IsFieldHidden_ShouldHide_WhenRuleExistsAndStateIsNull()
    {
        var template = Dummy();
        template.ConditionalLogic = [HideFieldRule("name")];
        var evaluator = Evaluator(template, conditionalState: null);

        Assert.True(evaluator.IsFieldHidden("name"));
        Assert.False(evaluator.IsFieldHidden("other"));
    }

    [Fact]
    public void IsFieldHidden_ShouldUseVisibilityMap()
    {
        var template = Dummy();
        template.ConditionalLogic = [HideFieldRule("name")];
        var state = new FormConditionalState { FieldVisibility = { ["name"] = false, ["ok"] = true } };
        var evaluator = Evaluator(template, state);

        Assert.True(evaluator.IsFieldHidden("name"));
        Assert.False(evaluator.IsFieldHidden("ok"));
    }

    [Fact]
    public void IsPageHidden_ShouldUseSkippedPagesAndVisibility()
    {
        var template = Dummy();
        template.ConditionalLogic = [HidePageRule("p1")];
        var state = new FormConditionalState
        {
            SkippedPages = { "skip-me" },
            PageVisibility = { ["p2"] = false }
        };
        var evaluator = Evaluator(template, state);

        Assert.True(evaluator.IsPageHidden("skip-me"));
        Assert.True(evaluator.IsPageHidden("p2"));
        Assert.True(evaluator.IsPageHidden("p1"));
        Assert.False(evaluator.IsPageHidden("p-open"));
    }

    [Fact]
    public void HasConditionalLogicShowingPages_ShouldEvaluateOperators()
    {
        var template = Dummy();
        template.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Enabled = true,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = "AND",
                    Conditions =
                    [
                        new Condition { TriggerField = "kind", Operator = "equals", Value = "yes" },
                        new Condition { TriggerField = "note", Operator = "contains", Value = "ok" }
                    ]
                },
                AffectedElements = [new AffectedElement { ElementId = "p2", ElementType = "page", Action = "show" }]
            }
        ];
        var evaluator = Evaluator(template, new FormConditionalState());

        Assert.True(evaluator.HasConditionalLogicShowingPages(new Dictionary<string, object>
        {
            ["kind"] = "yes",
            ["note"] = "looks ok"
        }));
        Assert.False(evaluator.HasConditionalLogicShowingPages(new Dictionary<string, object>
        {
            ["kind"] = "no",
            ["note"] = "looks ok"
        }));
    }

    [Fact]
    public void IsFieldHiddenForItem_ShouldUseOrchestratorResult()
    {
        var orchestrator = Substitute.For<IConditionalLogicOrchestrator>();
        orchestrator.ApplyFieldVisibilityAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(new FormConditionalState { FieldVisibility = { ["name"] = false } });

        var template = Dummy();
        template.ConditionalLogic = [HideFieldRule("name")];
        var evaluator = new FormEngineVisibilityEvaluator(
            template,
            new FormConditionalState(),
            orchestrator,
            "p1",
            "t1",
            NullLogger.Instance);

        Assert.True(evaluator.IsFieldHiddenForItem("name", new Dictionary<string, object> { ["name"] = "x" }));
        Assert.False(evaluator.IsFieldHiddenForItem("other", new Dictionary<string, object>()));
    }

    private static FormEngineVisibilityEvaluator Evaluator(FormTemplate template, FormConditionalState? conditionalState) =>
        new(template, conditionalState, Substitute.For<IConditionalLogicOrchestrator>(), "p1", "t1", NullLogger.Instance);

    private static FormTemplate Dummy() => FormEngineConstants.CreateDummyTemplate();

    private static ConditionalLogic HideFieldRule(string fieldId) =>
        new()
        {
            Enabled = true,
            ConditionGroup = new ConditionGroup { LogicalOperator = "AND", Conditions = [] },
            AffectedElements = [new AffectedElement { ElementId = fieldId, ElementType = "field", Action = "hide" }]
        };

    private static ConditionalLogic HidePageRule(string pageId) =>
        new()
        {
            Enabled = true,
            ConditionGroup = new ConditionGroup { LogicalOperator = "AND", Conditions = [] },
            AffectedElements = [new AffectedElement { ElementId = pageId, ElementType = "page", Action = "hide" }]
        };
}
