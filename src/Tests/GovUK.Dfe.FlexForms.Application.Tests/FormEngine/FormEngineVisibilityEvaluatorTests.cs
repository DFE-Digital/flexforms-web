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
    public void HasConditionalLogicShowingPages_ShouldMatchContainsAgainstMultiSelectValues()
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
                        new Condition
                        {
                            TriggerField = "significantChangeType",
                            Operator = "contains",
                            Value = "changeGenderComposition"
                        }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "gender-composition-flow-about-page",
                        ElementType = "page",
                        Action = "show"
                    }
                ]
            }
        ];
        var evaluator = Evaluator(template, new FormConditionalState());

        Assert.True(evaluator.HasConditionalLogicShowingPages(new Dictionary<string, object>
        {
            ["significantChangeType"] = new[] { "changeSatelliteSite", "changeGenderComposition" }
        }));
        Assert.False(evaluator.HasConditionalLogicShowingPages(new Dictionary<string, object>
        {
            ["significantChangeType"] = new[] { "changeSatelliteSite", "changeAgeRange" }
        }));
    }

    [Fact]
    public void HasConditionalLogicShowingPages_ShouldMatchEqualsAgainstMultiSelectCheckboxValues()
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
                        new Condition
                        {
                            TriggerField = "significantChangeType",
                            Operator = "equals",
                            Value = "changeAgeRange"
                        }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "age-range-flow-about-page",
                        ElementType = "page",
                        Action = "show"
                    }
                ]
            }
        ];
        var evaluator = Evaluator(template, new FormConditionalState());

        Assert.True(evaluator.HasConditionalLogicShowingPages(new Dictionary<string, object>
        {
            ["significantChangeType"] = new[] { "changeSatelliteSite", "changeAgeRange" }
        }));
        Assert.False(evaluator.HasConditionalLogicShowingPages(new Dictionary<string, object>
        {
            ["significantChangeType"] = new[] { "changeSatelliteSite", "changeGenderComposition" }
        }));
    }

    [Fact]
    public void HasConditionalLogicShowingPages_ShouldMatchContainsSubstring_OnSingleCheckboxValue()
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
                        new Condition
                        {
                            TriggerField = "significantChangeType",
                            Operator = "contains",
                            Value = "gender"
                        }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "gender-composition-flow-about-page",
                        ElementType = "page",
                        Action = "show"
                    }
                ]
            }
        ];
        var evaluator = Evaluator(template, new FormConditionalState());

        Assert.True(evaluator.HasConditionalLogicShowingPages(new Dictionary<string, object>
        {
            ["significantChangeType"] = "changeGenderComposition"
        }));
    }

    [Fact]
    public void HasConditionalLogicShowingPages_ShouldMatchJsonSerializedCheckboxArray()
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
                        new Condition
                        {
                            TriggerField = "significantChangeType",
                            Operator = "contains",
                            Value = "changeAgeRange"
                        }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "age-range-flow-about-page",
                        ElementType = "page",
                        Action = "show"
                    }
                ]
            }
        ];
        var evaluator = Evaluator(template, new FormConditionalState());

        Assert.True(evaluator.HasConditionalLogicShowingPages(new Dictionary<string, object>
        {
            ["significantChangeType"] = """["changeSatelliteSite","changeAgeRange"]"""
        }));
    }

    [Fact]
    public void HasConditionalLogicShowingPages_ShouldRespectNotContains_ForMultiSelectValues()
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
                        new Condition
                        {
                            TriggerField = "significantChangeType",
                            Operator = "not_contains",
                            Value = "changeGenderComposition"
                        }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "age-range-flow-about-page",
                        ElementType = "page",
                        Action = "show"
                    }
                ]
            }
        ];
        var evaluator = Evaluator(template, new FormConditionalState());

        Assert.True(evaluator.HasConditionalLogicShowingPages(new Dictionary<string, object>
        {
            ["significantChangeType"] = new[] { "changeSatelliteSite", "changeAgeRange" }
        }));
        Assert.False(evaluator.HasConditionalLogicShowingPages(new Dictionary<string, object>
        {
            ["significantChangeType"] = new[] { "changeSatelliteSite", "changeGenderComposition" }
        }));
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

    [Fact]
    public void GetNextPageRevealedByCurrentPageFields_ShouldReturnRevealedPage_WhenConditionMet()
    {
        var current = new Page
        {
            PageId = "p1",
            Slug = "p1",
            Title = "p1",
            Description = "p1",
            PageOrder = 1,
            Fields =
            [
                new Field
                {
                    FieldId = "hasSen",
                    Type = "radios",
                    Label = new Label { Value = "SEN?" },
                    Order = 1
                }
            ]
        };
        var revealed = new Page
        {
            PageId = "p2",
            Slug = "p2",
            Title = "p2",
            Description = "p2",
            PageOrder = 2,
            Fields = []
        };
        var alwaysVisible = new Page
        {
            PageId = "p3",
            Slug = "p3",
            Title = "p3",
            Description = "p3",
            PageOrder = 3,
            Fields = []
        };

        var template = Dummy();
        template.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Enabled = true,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = "AND",
                    Conditions = [new Condition { TriggerField = "hasSen", Operator = "equals", Value = "yes" }]
                },
                AffectedElements = [new AffectedElement { ElementId = "p2", ElementType = "page", Action = "show" }]
            }
        ];

        var evaluator = Evaluator(template, new FormConditionalState());
        var pages = new List<Page> { current, revealed, alwaysVisible };

        Assert.Equal(
            "p2",
            evaluator.GetNextPageRevealedByCurrentPageFields(
                current,
                new Dictionary<string, object> { ["hasSen"] = "yes" },
                pages));

        Assert.Null(
            evaluator.GetNextPageRevealedByCurrentPageFields(
                current,
                new Dictionary<string, object> { ["hasSen"] = "no" },
                pages));
    }

    [Fact]
    public void GetNextPageRevealedByCurrentPageFields_ShouldIgnoreRulesTriggeredByOtherPages()
    {
        var current = new Page
        {
            PageId = "p1",
            Slug = "p1",
            Title = "p1",
            Description = "p1",
            PageOrder = 1,
            Fields =
            [
                new Field
                {
                    FieldId = "name",
                    Type = "text",
                    Label = new Label { Value = "Name" },
                    Order = 1
                }
            ]
        };
        var later = new Page
        {
            PageId = "p2",
            Slug = "p2",
            Title = "p2",
            Description = "p2",
            PageOrder = 2,
            Fields = []
        };

        var template = Dummy();
        template.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Enabled = true,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = "AND",
                    Conditions = [new Condition { TriggerField = "otherField", Operator = "equals", Value = "yes" }]
                },
                AffectedElements = [new AffectedElement { ElementId = "p2", ElementType = "page", Action = "show" }]
            }
        ];

        var evaluator = Evaluator(template, new FormConditionalState());
        Assert.Null(
            evaluator.GetNextPageRevealedByCurrentPageFields(
                current,
                new Dictionary<string, object> { ["otherField"] = "yes", ["name"] = "Ada" },
                [current, later]));
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
