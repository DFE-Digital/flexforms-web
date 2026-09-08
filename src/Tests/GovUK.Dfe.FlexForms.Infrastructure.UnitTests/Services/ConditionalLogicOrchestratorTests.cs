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

    private static Page BuildPage(string pageId, int order, params string[] fieldIds) =>
        new()
        {
            PageId = pageId,
            Slug = pageId,
            Title = pageId,
            Description = string.Empty,
            PageOrder = order,
            Fields = fieldIds.Select((fieldId, fieldOrder) => new Field
            {
                FieldId = fieldId,
                Type = "text",
                Label = new Label { Value = fieldId },
                Order = fieldOrder,
                Required = false
            }).ToList()
        };

    private static ConditionalLogic PlaceholderRule() =>
        new()
        {
            Id = "placeholder",
            Enabled = true,
            Priority = 1,
            ConditionGroup = new ConditionGroup { LogicalOperator = "AND", Conditions = [] },
            AffectedElements = []
        };

    private static ConditionalLogic HidePageWhen(string ruleId, string triggerField, string triggerValue, string targetPage) =>
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
                    ElementId = targetPage,
                    ElementType = ConditionalLogicConstants.ElementTypes.Page,
                    Action = ConditionalLogicConstants.Actions.Hide
                }
            ]
        };

    private static ConditionalLogic HideFieldWhen(string ruleId, string triggerField, string triggerValue, string targetField) =>
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
                    Action = ConditionalLogicConstants.Actions.Hide
                }
            ]
        };

    private static ConditionalLogicAction CreateAction(
        string elementId,
        string elementType,
        string action,
        int priority = 1,
        Dictionary<string, object>? actionConfig = null) =>
        new()
        {
            RuleId = "rule-1",
            Priority = priority,
            Element = new AffectedElement
            {
                ElementId = elementId,
                ElementType = elementType,
                Action = action,
                ActionConfig = actionConfig
            }
        };

    private static IConditionalLogicEngine EngineReturningActions(params ConditionalLogicAction[] actions)
    {
        var engine = Substitute.For<IConditionalLogicEngine>();
        engine.EvaluateRules(
                Arg.Any<IEnumerable<ConditionalLogic>>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<ConditionalLogicContext?>())
            .Returns(new ConditionalLogicResult { Actions = actions.ToList() });
        return engine;
    }

    private static IConditionalLogicEngine ThrowingEngine()
    {
        var engine = Substitute.For<IConditionalLogicEngine>();
        engine.EvaluateRules(
                Arg.Any<IEnumerable<ConditionalLogic>>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<ConditionalLogicContext?>())
            .Returns(_ => throw new InvalidOperationException("engine failure"));
        return engine;
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldReturnEmptyState_WhenEngineThrows()
    {
        var template = CreateTemplateWithPages(("page-1", ["field-a"]));
        template.ConditionalLogic = [PlaceholderRule()];
        var orchestrator = CreateOrchestrator(ThrowingEngine());

        var state = await orchestrator.ApplyConditionalLogicAsync(template, []);

        Assert.Empty(state.FieldVisibility);
        Assert.Empty(state.PageVisibility);
        Assert.Null(state.EvaluationResult);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldSeedRequiredState_FromFieldDefinitions()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["required-field", "optional-field"]));
        template.TaskGroups[0].Tasks[0].Pages![0].Fields[0].Required = true;

        var state = await orchestrator.ApplyConditionalLogicAsync(template, []);

        Assert.True(state.FieldRequired["required-field"]);
        Assert.False(state.FieldRequired["optional-field"]);
        Assert.True(state.FieldEnabled["required-field"]);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldIncludeFlowAndDerivedFlowPages_InDefaultState()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["field-a"]));
        template.TaskGroups[0].Tasks[0].Summary = new TaskSummaryConfiguration
        {
            Flows = [new MultiCollectionFlowConfiguration { FlowId = "f1", Pages = [BuildPage("flow-page", 1, "flow-field")] }],
            DerivedFlows = [new DerivedCollectionFlowConfiguration { FlowId = "d1", Pages = [BuildPage("derived-page", 2, "derived-field")] }]
        };

        var state = await orchestrator.ApplyConditionalLogicAsync(template, []);

        Assert.True(state.PageVisibility["flow-page"]);
        Assert.True(state.PageVisibility["derived-page"]);
        Assert.True(state.FieldVisibility["flow-field"]);
        Assert.True(state.FieldVisibility["derived-field"]);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldTrackPagesWithoutFields()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", []));
        template.TaskGroups[0].Tasks[0].Pages![0].Fields = null!;

        var state = await orchestrator.ApplyConditionalLogicAsync(template, []);

        Assert.True(state.PageVisibility["page-1"]);
        Assert.Empty(state.FieldVisibility);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldSucceed_WhenTemplateIdIsNull()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["field-a"]));
        template.TemplateId = null!;

        var state = await orchestrator.ApplyConditionalLogicAsync(template, []);

        Assert.True(state.FieldVisibility["field-a"]);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldReuseTheCachedTemplateStructure_AcrossCalls()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["field-a"]));

        var first = await orchestrator.ApplyConditionalLogicAsync(template, []);
        template.TaskGroups[0].Tasks[0].Pages!.Add(BuildPage("page-2", 1, "field-b"));
        var second = await orchestrator.ApplyConditionalLogicAsync(template, []);

        Assert.True(first.FieldVisibility["field-a"]);
        Assert.DoesNotContain("field-b", second.FieldVisibility.Keys);
        Assert.DoesNotContain("page-2", second.PageVisibility.Keys);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldLeaveDefaultsIntact_ForRulesThatAffectNothingUsable()
    {
        var template = CreateTemplateWithPages(("page-1", ["field-a"]));
        template.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Id = "no-affected-elements",
                Enabled = true,
                ConditionGroup = new ConditionGroup { LogicalOperator = "AND", Conditions = [] },
                AffectedElements = null!
            },
            new ConditionalLogic
            {
                Id = "null-affected-element",
                Enabled = true,
                ConditionGroup = new ConditionGroup { LogicalOperator = "AND", Conditions = [] },
                AffectedElements = [null!]
            },
            new ConditionalLogic
            {
                Id = "unsupported-element-type",
                Enabled = true,
                ConditionGroup = new ConditionGroup { LogicalOperator = "AND", Conditions = [] },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "field-a",
                        ElementType = ConditionalLogicConstants.ElementTypes.Section,
                        Action = ConditionalLogicConstants.Actions.Show
                    }
                ]
            },
            new ConditionalLogic
            {
                Id = "disabled-rule",
                Enabled = false,
                ConditionGroup = new ConditionGroup { LogicalOperator = "AND", Conditions = [] },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "field-a",
                        ElementType = ConditionalLogicConstants.ElementTypes.Field,
                        Action = ConditionalLogicConstants.Actions.Hide
                    }
                ]
            }
        ];
        var orchestrator = CreateOrchestrator(EngineReturningActions());

        var state = await orchestrator.ApplyConditionalLogicAsync(template, []);

        Assert.True(state.FieldVisibility["field-a"]);
        Assert.True(state.PageVisibility["page-1"]);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldApplyPageShowAndHideActions_InPriorityOrder()
    {
        var engine = EngineReturningActions(
            CreateAction("page-1", ConditionalLogicConstants.ElementTypes.Page, ConditionalLogicConstants.Actions.Show, priority: 2),
            CreateAction("page-1", ConditionalLogicConstants.ElementTypes.Page, ConditionalLogicConstants.Actions.Hide, priority: 1),
            CreateAction("page-2", ConditionalLogicConstants.ElementTypes.Page, ConditionalLogicConstants.Actions.Hide, priority: 1));
        var template = CreateTemplateWithPages(("page-1", ["field-a"]), ("page-2", ["field-b"]));
        template.ConditionalLogic = [PlaceholderRule()];

        var state = await CreateOrchestrator(engine).ApplyConditionalLogicAsync(template, []);

        Assert.True(state.PageVisibility["page-1"]);
        Assert.False(state.PageVisibility["page-2"]);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldApplyEnableAndDisableActions()
    {
        var engine = EngineReturningActions(
            CreateAction("field-a", ConditionalLogicConstants.ElementTypes.Field, ConditionalLogicConstants.Actions.Disable, priority: 1),
            CreateAction("field-a", ConditionalLogicConstants.ElementTypes.Field, ConditionalLogicConstants.Actions.Enable, priority: 2),
            CreateAction("field-b", ConditionalLogicConstants.ElementTypes.Field, ConditionalLogicConstants.Actions.Disable, priority: 1));
        var template = CreateTemplateWithPages(("page-1", ["field-a", "field-b"]));
        template.ConditionalLogic = [PlaceholderRule()];

        var state = await CreateOrchestrator(engine).ApplyConditionalLogicAsync(template, []);

        Assert.True(state.FieldEnabled["field-a"]);
        Assert.False(state.FieldEnabled["field-b"]);
    }

    [Theory]
    [InlineData(ConditionalLogicConstants.Actions.MakeOptional)]
    [InlineData(ConditionalLogicConstants.Actions.SetValue)]
    [InlineData(ConditionalLogicConstants.Actions.ClearValue)]
    [InlineData(ConditionalLogicConstants.Actions.AddValidation)]
    [InlineData(ConditionalLogicConstants.Actions.RemoveValidation)]
    [InlineData(ConditionalLogicConstants.Actions.ShowMessage)]
    public async Task ApplyConditionalLogicAsync_ShouldLeaveStateUnchanged_ForCamelCasedActionNames(string action)
    {
        // The action switch compares a lower-cased action name against the camelCased action
        // constants, so these actions never match and are treated as unknown.
        var engine = EngineReturningActions(
            CreateAction(
                "required-field",
                ConditionalLogicConstants.ElementTypes.Field,
                action,
                actionConfig: new Dictionary<string, object>
                {
                    ["value"] = "preset",
                    ["validationType"] = "required",
                    ["message"] = "Now required"
                }));
        var template = CreateTemplateWithPages(("page-1", ["required-field"]));
        template.TaskGroups[0].Tasks[0].Pages![0].Fields[0].Required = true;
        template.ConditionalLogic = [PlaceholderRule()];

        var state = await CreateOrchestrator(engine).ApplyConditionalLogicAsync(template, []);

        Assert.True(state.FieldRequired["required-field"]);
        Assert.Empty(state.FieldValues);
        Assert.Empty(state.AdditionalValidations);
        Assert.Empty(state.Messages);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldIgnoreFieldOnlyActions_WhenTargetIsAPage()
    {
        var engine = EngineReturningActions(
            CreateAction("page-1", ConditionalLogicConstants.ElementTypes.Page, ConditionalLogicConstants.Actions.Require),
            CreateAction("page-1", ConditionalLogicConstants.ElementTypes.Page, ConditionalLogicConstants.Actions.Disable),
            CreateAction("field-a", ConditionalLogicConstants.ElementTypes.Field, ConditionalLogicConstants.Actions.Skip));
        var template = CreateTemplateWithPages(("page-1", ["field-a"]));
        template.ConditionalLogic = [PlaceholderRule()];

        var state = await CreateOrchestrator(engine).ApplyConditionalLogicAsync(template, []);

        Assert.Empty(state.SkippedPages);
        Assert.False(state.FieldRequired["field-a"]);
        Assert.True(state.FieldEnabled["field-a"]);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldLeaveStateUnchanged_WhenActionIsUnknown()
    {
        var engine = EngineReturningActions(
            CreateAction("field-a", ConditionalLogicConstants.ElementTypes.Field, "teleport"));
        var template = CreateTemplateWithPages(("page-1", ["field-a"]));
        template.ConditionalLogic = [PlaceholderRule()];

        var state = await CreateOrchestrator(engine).ApplyConditionalLogicAsync(template, []);

        Assert.True(state.FieldVisibility["field-a"]);
        Assert.Empty(state.FieldValues);
        Assert.Empty(state.Messages);
    }

    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldContinueApplyingActions_WhenOneActionThrows()
    {
        var faultyAction = CreateAction("field-a", ConditionalLogicConstants.ElementTypes.Field, ConditionalLogicConstants.Actions.Hide);
        faultyAction.Element.Action = null!;

        var engine = EngineReturningActions(
            faultyAction,
            CreateAction("field-b", ConditionalLogicConstants.ElementTypes.Field, ConditionalLogicConstants.Actions.Hide, priority: 2));
        var template = CreateTemplateWithPages(("page-1", ["field-a", "field-b"]));
        template.ConditionalLogic = [PlaceholderRule()];

        var state = await CreateOrchestrator(engine).ApplyConditionalLogicAsync(template, []);

        Assert.True(state.FieldVisibility["field-a"]);
        Assert.False(state.FieldVisibility["field-b"]);
    }

    [Fact]
    public async Task ApplyFieldVisibilityAsync_ShouldReturnEmptyState_WhenNoFieldHasVisibilityRules()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["field-a"]));

        var state = await orchestrator.ApplyFieldVisibilityAsync(template, []);

        Assert.Empty(state.FieldVisibility);
        Assert.Null(state.EvaluationResult);
    }

    [Fact]
    public async Task ApplyFieldVisibilityAsync_ShouldIgnoreRequestedFieldsWithoutVisibilityRules()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["trigger", "shown", "unmanaged"]));
        template.ConditionalLogic = [ShowFieldWhen("show", "trigger", "yes", "shown")];

        var state = await orchestrator.ApplyFieldVisibilityAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "yes" },
            ["unmanaged"]);

        Assert.Empty(state.FieldVisibility);
    }

    [Fact]
    public async Task ApplyFieldVisibilityAsync_ShouldOnlyEvaluateRulesThatTargetTheRequestedFields()
    {
        var engine = EngineReturningActions();
        var template = CreateTemplateWithPages(("page-1", ["trigger", "shown"]), ("page-2", []));
        template.ConditionalLogic =
        [
            ShowFieldWhen("show-shown", "trigger", "yes", "shown"),
            HidePageWhen("hide-page-2", "trigger", "yes", "page-2")
        ];

        await CreateOrchestrator(engine).ApplyFieldVisibilityAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "yes" });

        engine.Received(1).EvaluateRules(
            Arg.Is<IEnumerable<ConditionalLogic>>(rules => rules.Single().Id == "show-shown"),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<ConditionalLogicContext?>());
    }

    [Fact]
    public async Task ApplyFieldVisibilityAsync_ShouldIgnoreActionsForElementsOutsideTheTargetSet()
    {
        var engine = EngineReturningActions(
            CreateAction("page-1", ConditionalLogicConstants.ElementTypes.Page, ConditionalLogicConstants.Actions.Show),
            CreateAction("other-field", ConditionalLogicConstants.ElementTypes.Field, ConditionalLogicConstants.Actions.Show));
        var template = CreateTemplateWithPages(("page-1", ["trigger", "shown"]));
        template.ConditionalLogic = [ShowFieldWhen("show-shown", "trigger", "yes", "shown")];

        var state = await CreateOrchestrator(engine).ApplyFieldVisibilityAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "yes" });

        Assert.False(state.FieldVisibility["shown"]);
        Assert.DoesNotContain("other-field", state.FieldVisibility.Keys);
        Assert.DoesNotContain("page-1", state.FieldVisibility.Keys);
    }

    [Fact]
    public async Task ApplyFieldVisibilityAsync_ShouldIgnoreActionsThatAreNotShowOrHide()
    {
        var engine = EngineReturningActions(
            CreateAction("shown", ConditionalLogicConstants.ElementTypes.Field, ConditionalLogicConstants.Actions.Require));
        var template = CreateTemplateWithPages(("page-1", ["trigger", "shown"]));
        template.ConditionalLogic = [ShowFieldWhen("show-shown", "trigger", "yes", "shown")];

        var state = await CreateOrchestrator(engine).ApplyFieldVisibilityAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "yes" });

        Assert.False(state.FieldVisibility["shown"]);
    }

    [Fact]
    public async Task ApplyFieldVisibilityAsync_ShouldKeepFieldsHidden_WhenEngineThrows()
    {
        var template = CreateTemplateWithPages(("page-1", ["trigger", "shown"]));
        template.ConditionalLogic = [ShowFieldWhen("show-shown", "trigger", "yes", "shown")];

        var state = await CreateOrchestrator(ThrowingEngine()).ApplyFieldVisibilityAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "yes" });

        Assert.False(state.FieldVisibility["shown"]);
        Assert.Null(state.EvaluationResult);
    }

    [Fact]
    public async Task EvaluateFieldChangeAsync_ShouldReturnAnError_WhenEngineThrows()
    {
        var engine = Substitute.For<IConditionalLogicEngine>();
        engine.GetTriggeredRules(Arg.Any<IEnumerable<ConditionalLogic>>(), Arg.Any<string>())
            .Returns(_ => throw new InvalidOperationException("engine failure"));
        var template = CreateTemplateWithPages(("page-1", ["field-a"]));
        template.ConditionalLogic = [PlaceholderRule()];

        var result = await CreateOrchestrator(engine).EvaluateFieldChangeAsync(template, [], "field-a");

        Assert.Equal("Error evaluating field change: engine failure", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task GetElementVisibilityAsync_ShouldReturnEmpty_WhenTemplateHasNoPages()
    {
        var orchestrator = CreateOrchestrator();

        var visibility = await orchestrator.GetElementVisibilityAsync(CreateTemplateWithPages(), []);

        Assert.Empty(visibility);
    }

    [Fact]
    public async Task GetFieldRequiredStateAsync_ShouldReflectTemplateRequiredFlags_WhenNoRulesExist()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["required-field", "optional-field"]));
        template.TaskGroups[0].Tasks[0].Pages![0].Fields[0].Required = true;

        var required = await orchestrator.GetFieldRequiredStateAsync(template, []);

        Assert.True(required["required-field"]);
        Assert.False(required["optional-field"]);
    }

    [Fact]
    public async Task ValidateTemplateRulesAsync_ShouldReturnAnInvalidResult_WhenEngineThrows()
    {
        var engine = Substitute.For<IConditionalLogicEngine>();
        engine.ValidateRule(Arg.Any<ConditionalLogic>())
            .Returns(_ => throw new InvalidOperationException("engine failure"));
        var template = CreateTemplateWithPages(("page-1", ["field-a"]));
        template.ConditionalLogic = [PlaceholderRule()];

        var results = await CreateOrchestrator(engine).ValidateTemplateRulesAsync(template);

        var result = Assert.Single(results);
        Assert.False(result.IsValid);
        Assert.Equal("Validation error: engine failure", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task GetNextPageAsync_ShouldReturnNull_WhenCurrentPageIsNotInTheTemplate()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["field-a"]), ("page-2", ["field-b"]));

        Assert.Null(await orchestrator.GetNextPageAsync(template, [], "page-unknown"));
    }

    [Fact]
    public async Task GetNextPageAsync_ShouldReturnTheFollowingPage_WhenItIsVisible()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["field-a"]), ("page-2", ["field-b"]));

        Assert.Equal("page-2", await orchestrator.GetNextPageAsync(template, [], "page-1"));
    }

    [Fact]
    public async Task GetNextPageAsync_ShouldSkipPagesHiddenByConditionalLogic()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(
            ("page-1", ["trigger"]),
            ("page-2", ["field-b"]),
            ("page-3", ["field-c"]));
        template.ConditionalLogic = [HidePageWhen("hide-page-2", "trigger", "hide", "page-2")];

        var nextPage = await orchestrator.GetNextPageAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "hide" },
            "page-1");

        Assert.Equal("page-3", nextPage);
    }

    [Fact]
    public async Task GetNextPageAsync_ShouldSkipPagesWhereEveryFieldIsHidden()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(
            ("page-1", ["trigger"]),
            ("page-2", ["field-b"]),
            ("page-3", ["field-c"]));
        template.ConditionalLogic = [HideFieldWhen("hide-field-b", "trigger", "hide", "field-b")];

        var nextPage = await orchestrator.GetNextPageAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "hide" },
            "page-1");

        Assert.Equal("page-3", nextPage);
    }

    [Fact]
    public async Task GetNextPageAsync_ShouldReturnPagesThatHaveNoFields()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["field-a"]), ("page-2", []));

        Assert.Equal("page-2", await orchestrator.GetNextPageAsync(template, [], "page-1"));
    }

    [Fact]
    public async Task GetNextPageAsync_ShouldReturnNull_WhenNoRemainingPageIsVisible()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["trigger"]), ("page-2", ["field-b"]));
        template.ConditionalLogic = [HidePageWhen("hide-page-2", "trigger", "hide", "page-2")];

        var nextPage = await orchestrator.GetNextPageAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "hide" },
            "page-1");

        Assert.Null(nextPage);
    }

    [Fact]
    public async Task ShouldSkipPageAsync_ShouldReturnTrue_WhenPageIsHiddenByConditionalLogic()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["trigger"]), ("page-2", ["field-b"]));
        template.ConditionalLogic = [HidePageWhen("hide-page-2", "trigger", "hide", "page-2")];

        var shouldSkip = await orchestrator.ShouldSkipPageAsync(
            template,
            new Dictionary<string, object> { ["trigger"] = "hide" },
            "page-2");

        Assert.True(shouldSkip);
    }

    [Fact]
    public async Task ShouldSkipPageAsync_ShouldReturnFalse_WhenPageIsNotInTheTemplate()
    {
        var orchestrator = CreateOrchestrator();
        var template = CreateTemplateWithPages(("page-1", ["field-a"]));

        Assert.False(await orchestrator.ShouldSkipPageAsync(template, [], "page-unknown"));
    }
}
