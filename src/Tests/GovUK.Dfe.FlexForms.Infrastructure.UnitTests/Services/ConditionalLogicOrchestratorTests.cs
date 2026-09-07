using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class ConditionalLogicOrchestratorTests
{
    private readonly ConditionalLogicEngine _realEngine =
        new(NullLogger<ConditionalLogicEngine>.Instance);

    private ConditionalLogicOrchestrator CreateOrchestrator(IConditionalLogicEngine? engine = null) =>
        new(engine ?? _realEngine, NullLogger<ConditionalLogicOrchestrator>.Instance);

    private static FormTemplate CreateTemplateWithPages(params (string PageId, string[] FieldIds)[] pages)
    {
        var pageModels = pages.Select((p, index) => new Page
        {
            PageId = p.PageId,
            Slug = p.PageId,
            Title = p.PageId,
            Description = string.Empty,
            PageOrder = index,
            Fields = p.FieldIds.Select((fieldId, fieldOrder) => new Field
            {
                FieldId = fieldId,
                Type = "text",
                Label = new Label { Value = fieldId },
                Order = fieldOrder,
                Required = false
            }).ToList()
        }).ToList();

        return new FormTemplate
        {
            TemplateId = "test-template",
            TemplateName = "Test template",
            Description = "Test",
            TaskGroups =
            [
                new TaskGroup
                {
                    GroupId = "g1",
                    GroupName = "Group",
                    GroupOrder = 1,
                    GroupStatus = "NotStarted",
                    Tasks =
                    [
                        new Domain.Models.Task
                        {
                            TaskId = "t1",
                            TaskName = "Task",
                            TaskOrder = 1,
                            TaskStatusString = "NotStarted",
                            Pages = pageModels
                        }
                    ]
                }
            ]
        };
    }

    private static ConditionalLogic ShowFieldWhen(string ruleId, string triggerField, string triggerValue, string targetField) =>
        new()
        {
            Id = ruleId,
            Enabled = true,
            Priority = 1,
            ConditionGroup = new ConditionGroup
            {
                LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                Conditions =
                [
                    new Condition
                    {
                        TriggerField = triggerField,
                        Operator = ConditionalLogicConstants.Operators.Equals,
                        Value = triggerValue,
                        DataType = ConditionalLogicConstants.DataTypes.String
                    }
                ]
            },
            AffectedElements =
            [
                new AffectedElement
                {
                    ElementId = targetField,
                    ElementType = ConditionalLogicConstants.ElementTypes.Field,
                    Action = ConditionalLogicConstants.Actions.Show
                }
            ]
        };

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldReturnDefaultState_WhenNoRules()
    {
        var engine = Substitute.For<IConditionalLogicEngine>();
        var orchestrator = CreateOrchestrator(engine);
        var template = CreateTemplateWithPages(("p1", ["field-a"]));

        var state = await orchestrator.ApplyConditionalLogicAsync(template, []);

        Assert.True(state.FieldVisibility["field-a"]);
        engine.DidNotReceiveWithAnyArgs().EvaluateRules(default!, default!, default);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldApplyShowAction_FromEngineResult()
    {
        var engine = Substitute.For<IConditionalLogicEngine>();
        engine.EvaluateRules(Arg.Any<IEnumerable<ConditionalLogic>>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<ConditionalLogicContext?>())
            .Returns(new ConditionalLogicResult
            {
                Actions =
                [
                    new ConditionalLogicAction
                    {
                        RuleId = "show-details",
                        Priority = 1,
                        Element = new AffectedElement
                        {
                            ElementId = "details",
                            ElementType = ConditionalLogicConstants.ElementTypes.Field,
                            Action = ConditionalLogicConstants.Actions.Show
                        }
                    }
                ]
            });

        var orchestrator = CreateOrchestrator(engine);
        var template = CreateTemplateWithPages(("p1", ["details"]));
        template.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Id = "show-details",
                Enabled = true,
                ConditionGroup = new ConditionGroup { LogicalOperator = "AND", Conditions = [] },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "details",
                        ElementType = ConditionalLogicConstants.ElementTypes.Field,
                        Action = ConditionalLogicConstants.Actions.Show
                    }
                ]
            }
        ];

        var state = await orchestrator.ApplyConditionalLogicAsync(template, []);

        Assert.True(state.FieldVisibility["details"]);
        Assert.NotNull(state.EvaluationResult);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldApplyHideRequireSetValueAndSkipActions()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(
            ("page-1", ["trigger", "hidden-field", "required-field", "value-field"]),
            ("page-2", ["other-field"]));
        template.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Id = "multi-action",
                Enabled = true,
                Priority = 1,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions =
                    [
                        new Condition
                        {
                            TriggerField = "trigger",
                            Operator = ConditionalLogicConstants.Operators.Equals,
                            Value = "go",
                            DataType = ConditionalLogicConstants.DataTypes.String
                        }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "hidden-field",
                        ElementType = ConditionalLogicConstants.ElementTypes.Field,
                        Action = ConditionalLogicConstants.Actions.Hide
                    },
                    new AffectedElement
                    {
                        ElementId = "required-field",
                        ElementType = ConditionalLogicConstants.ElementTypes.Field,
                        Action = ConditionalLogicConstants.Actions.Require
                    },
                    new AffectedElement
                    {
                        ElementId = "value-field",
                        ElementType = ConditionalLogicConstants.ElementTypes.Field,
                        Action = ConditionalLogicConstants.Actions.SetValue,
                        ActionConfig = new Dictionary<string, object> { ["value"] = "preset" }
                    },
                    new AffectedElement
                    {
                        ElementId = "page-2",
                        ElementType = ConditionalLogicConstants.ElementTypes.Page,
                        Action = ConditionalLogicConstants.Actions.Skip
                    }
                ]
            }
        ];

        var state = await orchestrator.ApplyConditionalLogicAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "go" });

        Assert.False(state.FieldVisibility["hidden-field"]);
        Assert.True(state.FieldRequired["required-field"]);
        Assert.Contains("page-2", state.SkippedPages);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_AffectedFieldsDefaultHiddenUntilShown()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("p1", ["trigger", "conditional-field"]));
        template.ConditionalLogic = [ShowFieldWhen("show", "trigger", "yes", "conditional-field")];

        var hiddenState = await orchestrator.ApplyConditionalLogicAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "no" });
        var visibleState = await orchestrator.ApplyConditionalLogicAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "yes" });

        Assert.False(hiddenState.FieldVisibility["conditional-field"]);
        Assert.True(visibleState.FieldVisibility["conditional-field"]);
    }

    [Fact]
    public async Task ApplyFieldVisibilityAsync_ShouldApplyShowAndHideForTargetFields()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("p1", ["trigger", "shown", "hidden"]));
        template.ConditionalLogic =
        [
            ShowFieldWhen("show-shown", "trigger", "yes", "shown"),
            new ConditionalLogic
            {
                Id = "hide-hidden",
                Enabled = true,
                Priority = 2,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions =
                    [
                        new Condition
                        {
                            TriggerField = "trigger",
                            Operator = ConditionalLogicConstants.Operators.Equals,
                            Value = "yes",
                            DataType = ConditionalLogicConstants.DataTypes.String
                        }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "hidden",
                        ElementType = ConditionalLogicConstants.ElementTypes.Field,
                        Action = ConditionalLogicConstants.Actions.Hide
                    }
                ]
            }
        ];

        var state = await orchestrator.ApplyFieldVisibilityAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "yes" },
            ["shown", "hidden"]);

        Assert.True(state.FieldVisibility["shown"]);
        Assert.False(state.FieldVisibility["hidden"]);
    }

    [Fact]
    public async Task ApplyFieldVisibilityAsync_ShouldUseAllVisibilityFields_WhenFieldIdsNull()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("p1", ["trigger", "shown"]));
        template.ConditionalLogic = [ShowFieldWhen("show", "trigger", "yes", "shown")];

        var state = await orchestrator.ApplyFieldVisibilityAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "yes" });

        Assert.True(state.FieldVisibility["shown"]);
    }

    [Fact]
    public async Task EvaluateFieldChangeAsync_ShouldEvaluateTriggeredRulesOnly()
    {
        var engine = Substitute.For<IConditionalLogicEngine>();
        var triggered = new[]
        {
            new ConditionalLogic
            {
                Id = "on-change",
                Enabled = true,
                ConditionGroup = new ConditionGroup { LogicalOperator = "AND", Conditions = [] },
                AffectedElements = []
            }
        };
        engine.GetTriggeredRules(Arg.Any<IEnumerable<ConditionalLogic>>(), "amount").Returns(triggered);
        engine.EvaluateRules(triggered, Arg.Any<Dictionary<string, object>>(), Arg.Any<ConditionalLogicContext?>())
            .Returns(new ConditionalLogicResult { EvaluatedRules = ["on-change"] });

        var orchestrator = CreateOrchestrator(engine);
        var template = FormEngineConstants.CreateDummyTemplate();
        template.ConditionalLogic = triggered.ToList();

        var result = await orchestrator.EvaluateFieldChangeAsync(template, [], "amount");

        Assert.Equal(["on-change"], result.EvaluatedRules);
        engine.Received(1).GetTriggeredRules(template.ConditionalLogic, "amount");
    }

    [Fact]
    public async Task EvaluateFieldChangeAsync_ShouldReturnEmpty_WhenNoConditionalLogic()
    {
        var orchestrator = CreateOrchestrator();
        var template = FormEngineConstants.CreateDummyTemplate();

        var result = await orchestrator.EvaluateFieldChangeAsync(template, [], "field");

        Assert.Empty(result.EvaluatedRules);
        Assert.Empty(result.Actions);
    }

    [Fact]
    public async Task GetElementVisibilityAsync_ShouldCombineFieldAndPageVisibility()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["trigger", "field-a"]), ("page-2", []));
        template.ConditionalLogic =
        [
            ShowFieldWhen("show-field", "trigger", "yes", "field-a"),
            new ConditionalLogic
            {
                Id = "show-page",
                Enabled = true,
                Priority = 2,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions =
                    [
                        new Condition
                        {
                            TriggerField = "trigger",
                            Operator = ConditionalLogicConstants.Operators.Equals,
                            Value = "yes",
                            DataType = ConditionalLogicConstants.DataTypes.String
                        }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "page-2",
                        ElementType = ConditionalLogicConstants.ElementTypes.Page,
                        Action = ConditionalLogicConstants.Actions.Show
                    }
                ]
            }
        ];

        var visibility = await orchestrator.GetElementVisibilityAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "yes" });

        Assert.True(visibility["field-a"]);
        Assert.True(visibility["page-2"]);
    }

    [Fact]
    public async Task GetFieldRequiredStateAsync_ShouldReturnRequiredFields()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("p1", ["trigger", "required-field"]));
        template.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Id = "require",
                Enabled = true,
                Priority = 1,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions =
                    [
                        new Condition
                        {
                            TriggerField = "trigger",
                            Operator = ConditionalLogicConstants.Operators.Equals,
                            Value = "yes",
                            DataType = ConditionalLogicConstants.DataTypes.String
                        }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "required-field",
                        ElementType = ConditionalLogicConstants.ElementTypes.Field,
                        Action = ConditionalLogicConstants.Actions.Require
                    }
                ]
            }
        ];

        var required = await orchestrator.GetFieldRequiredStateAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "yes" });

        Assert.True(required["required-field"]);
    }

    [Fact]
    public async Task ValidateTemplateRulesAsync_ShouldReturnValidationResults()
    {
        var engine = Substitute.For<IConditionalLogicEngine>();
        engine.ValidateRule(Arg.Any<ConditionalLogic>())
            .Returns(new ConditionalLogicValidationResult { IsValid = true });

        var orchestrator = CreateOrchestrator(engine);
        var template = FormEngineConstants.CreateDummyTemplate();
        template.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Id = "rule-1",
                Enabled = true,
                ConditionGroup = new ConditionGroup { LogicalOperator = "AND", Conditions = [] },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "field-1",
                        ElementType = ConditionalLogicConstants.ElementTypes.Field,
                        Action = ConditionalLogicConstants.Actions.Show
                    }
                ]
            }
        ];

        var results = await orchestrator.ValidateTemplateRulesAsync(template);

        Assert.Single(results);
        Assert.True(results[0].IsValid);
    }

    [Fact]
    public async Task ValidateTemplateRulesAsync_ShouldReturnEmpty_WhenNoRules()
    {
        var orchestrator = CreateOrchestrator();
        var template = FormEngineConstants.CreateDummyTemplate();

        var results = await orchestrator.ValidateTemplateRulesAsync(template);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetNextPageAsync_ShouldSkipHiddenAndSkippedPages()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(
            ("page-1", ["trigger"]),
            ("page-2", ["field-b"]),
            ("page-3", ["field-c"]));
        template.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Id = "skip-page-2",
                Enabled = true,
                Priority = 1,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions =
                    [
                        new Condition
                        {
                            TriggerField = "trigger",
                            Operator = ConditionalLogicConstants.Operators.Equals,
                            Value = "skip",
                            DataType = ConditionalLogicConstants.DataTypes.String
                        }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "page-2",
                        ElementType = ConditionalLogicConstants.ElementTypes.Page,
                        Action = ConditionalLogicConstants.Actions.Skip
                    }
                ]
            }
        ];

        var nextPage = await orchestrator.GetNextPageAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "skip" },
            "page-1");

        Assert.Equal("page-3", nextPage);
    }

    [Fact]
    public async Task GetNextPageAsync_ShouldReturnNull_OnLastPage()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["field-a"]), ("page-2", ["field-b"]));

        var nextPage = await orchestrator.GetNextPageAsync(template, [], "page-2");

        Assert.Null(nextPage);
    }

    [Fact]
    public async Task ShouldSkipPageAsync_ShouldReturnTrue_WhenPageSkippedOrHidden()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["trigger"]), ("page-2", []));
        template.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Id = "skip",
                Enabled = true,
                Priority = 1,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions =
                    [
                        new Condition
                        {
                            TriggerField = "trigger",
                            Operator = ConditionalLogicConstants.Operators.Equals,
                            Value = "yes",
                            DataType = ConditionalLogicConstants.DataTypes.String
                        }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "page-2",
                        ElementType = ConditionalLogicConstants.ElementTypes.Page,
                        Action = ConditionalLogicConstants.Actions.Skip
                    }
                ]
            }
        ];

        var shouldSkip = await orchestrator.ShouldSkipPageAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "yes" },
            "page-2");

        Assert.True(shouldSkip);
    }

    [Fact]
    public async Task ShouldSkipPageAsync_ShouldReturnFalse_WhenPageVisible()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["field-a"]), ("page-2", ["field-b"]));

        var shouldSkip = await orchestrator.ShouldSkipPageAsync(template, [], "page-2");

        Assert.False(shouldSkip);
    }
}
