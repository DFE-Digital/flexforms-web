using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class ConditionalLogicEngineTests
{
    private readonly ConditionalLogicEngine _engine =
        new(NullLogger<ConditionalLogicEngine>.Instance);

    private static Condition SimpleCondition(
        string field,
        string op,
        object value,
        string dataType = ConditionalLogicConstants.DataTypes.String) =>
        new()
        {
            TriggerField = field,
            Operator = op,
            Value = value,
            DataType = dataType
        };

    private static ConditionalLogic Rule(
        string id,
        ConditionGroup group,
        IEnumerable<AffectedElement>? elements = null,
        bool enabled = true,
        int priority = 1) =>
        new()
        {
            Id = id,
            Enabled = enabled,
            Priority = priority,
            ConditionGroup = group,
            AffectedElements = elements?.ToList() ??
            [
                new AffectedElement
                {
                    ElementId = "target",
                    ElementType = ConditionalLogicConstants.ElementTypes.Field,
                    Action = ConditionalLogicConstants.Actions.Show
                }
            ]
        };

    [Fact]
    public void EvaluateRules_ShouldAddActions_WhenConditionMatches()
    {
        var rules = new[]
        {
            Rule(
                "show-name",
                new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions =
                    [
                        SimpleCondition("kind", ConditionalLogicConstants.Operators.Equals, "yes")
                    ]
                },
                [
                    new AffectedElement
                    {
                        ElementId = "details",
                        ElementType = ConditionalLogicConstants.ElementTypes.Field,
                        Action = ConditionalLogicConstants.Actions.Show
                    }
                ])
        };

        var result = _engine.EvaluateRules(
            rules,
            new Dictionary<string, object> { ["kind"] = "yes" });

        Assert.Contains("show-name", result.EvaluatedRules);
        Assert.Single(result.Actions);
        Assert.Equal("details", result.Actions[0].Element.ElementId);
    }

    [Fact]
    public void EvaluateRules_ShouldSkipDisabledRules()
    {
        var rules = new[]
        {
            Rule(
                "disabled",
                new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions = []
                },
                enabled: false)
        };

        var result = _engine.EvaluateRules(rules, []);

        Assert.Empty(result.EvaluatedRules);
        Assert.Empty(result.Actions);
    }

    [Fact]
    public void EvaluateRules_ShouldSortActionsByPriority()
    {
        var rules = new[]
        {
            Rule(
                "low-priority",
                new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions = [SimpleCondition("x", ConditionalLogicConstants.Operators.Equals, "value")]
                },
                [Affected("a")],
                priority: 10),
            Rule(
                "high-priority",
                new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions = [SimpleCondition("x", ConditionalLogicConstants.Operators.Equals, "value")]
                },
                [Affected("b")],
                priority: 1)
        };

        var result = _engine.EvaluateRules(rules, new Dictionary<string, object> { ["x"] = "value" });

        Assert.Equal(["b", "a"], result.Actions.Select(a => a.Element.ElementId));
    }

    [Fact]
    public void EvaluateCondition_StringEquals_IsCaseInsensitive()
    {
        var condition = SimpleCondition("field", ConditionalLogicConstants.Operators.Equals, "hello");
        Assert.True(_engine.EvaluateCondition(condition, new Dictionary<string, object> { ["field"] = "Hello" }));
    }

    [Fact]
    public void EvaluateCondition_NotEquals_DoesNotMatchLowercasedSwitchCase()
    {
        var condition = SimpleCondition("field", ConditionalLogicConstants.Operators.NotEquals, "world");
        Assert.False(_engine.EvaluateCondition(condition, new Dictionary<string, object> { ["field"] = "hello" }));
    }

    [Fact]
    public void EvaluateCondition_Contains_WorksForLowercaseOperator()
    {
        Assert.True(_engine.EvaluateCondition(
            SimpleCondition("field", ConditionalLogicConstants.Operators.Contains, "world"),
            new Dictionary<string, object> { ["field"] = "hello world" }));
    }

    [Fact]
    public void EvaluateCondition_IsEmpty_ReturnsFalseForCamelCaseOperator()
    {
        Assert.False(_engine.EvaluateCondition(
            SimpleCondition("field", ConditionalLogicConstants.Operators.IsEmpty, string.Empty),
            new Dictionary<string, object> { ["field"] = string.Empty }));
    }

    [Fact]
    public void EvaluateCondition_NumberEquals_UsesNumericComparison()
    {
        var equals = SimpleCondition("amount", ConditionalLogicConstants.Operators.Equals, "42", ConditionalLogicConstants.DataTypes.Number);
        Assert.True(_engine.EvaluateCondition(equals, new Dictionary<string, object> { ["amount"] = "42" }));
        Assert.False(_engine.EvaluateCondition(equals, new Dictionary<string, object> { ["amount"] = "99" }));
    }
    [Fact]
    public void EvaluateCondition_DateEquals_Works()
    {
        Assert.True(_engine.EvaluateCondition(
            SimpleCondition("startDate", ConditionalLogicConstants.Operators.Equals, "2024-06-01", ConditionalLogicConstants.DataTypes.Date),
            new Dictionary<string, object> { ["startDate"] = "2024-06-01" }));
        Assert.False(_engine.EvaluateCondition(
            SimpleCondition("startDate", ConditionalLogicConstants.Operators.Equals, "2024-06-01", ConditionalLogicConstants.DataTypes.Date),
            new Dictionary<string, object> { ["startDate"] = "2024-06-15" }));
    }
    [Fact]
    public void EvaluateCondition_BooleanEquals()
    {
        var condition = SimpleCondition(
            "agree",
            ConditionalLogicConstants.Operators.Equals,
            "true",
            ConditionalLogicConstants.DataTypes.Boolean);

        Assert.True(_engine.EvaluateCondition(condition, new Dictionary<string, object> { ["agree"] = "true" }));
        Assert.False(_engine.EvaluateCondition(condition, new Dictionary<string, object> { ["agree"] = "false" }));
    }

    [Fact]
    public void EvaluateCondition_IsTrueAndIsFalse_ReturnFalseForCamelCaseOperators()
    {
        var isTrue = SimpleCondition("flag", ConditionalLogicConstants.Operators.IsTrue, string.Empty);
        Assert.False(_engine.EvaluateCondition(isTrue, new Dictionary<string, object> { ["flag"] = "yes" }));
    }

    [Fact]
    public void EvaluateCondition_In_WithCommaSeparatedValues()
    {
        var condition = SimpleCondition(
            "colour",
            ConditionalLogicConstants.Operators.In,
            "red, green, blue");

        Assert.True(_engine.EvaluateCondition(
            condition,
            new Dictionary<string, object> { ["colour"] = "green" }));
        Assert.False(_engine.EvaluateCondition(
            condition,
            new Dictionary<string, object> { ["colour"] = "yellow" }));
    }

    [Fact]
    public void EvaluateCondition_In_WithJsonArray()
    {
        using var doc = JsonDocument.Parse("[\"a\",\"b\"]");
        var condition = SimpleCondition(
            "letter",
            ConditionalLogicConstants.Operators.In,
            doc.RootElement);

        Assert.True(_engine.EvaluateCondition(
            condition,
            new Dictionary<string, object> { ["letter"] = "b" }));
    }

    [Fact]
    public void EvaluateCondition_NotIn_ReturnsFalseForCamelCaseOperator()
    {
        var condition = SimpleCondition(
            "status",
            ConditionalLogicConstants.Operators.NotIn,
            "draft,pending");

        Assert.False(_engine.EvaluateCondition(
            condition,
            new Dictionary<string, object> { ["status"] = "approved" }));
    }

    [Fact]
    public void EvaluateCondition_Between_WithJsonArray()
    {
        using var doc = JsonDocument.Parse("[5, 15]");
        var condition = SimpleCondition(
            "score",
            ConditionalLogicConstants.Operators.Between,
            doc.RootElement,
            ConditionalLogicConstants.DataTypes.Number);

        Assert.True(_engine.EvaluateCondition(
            condition,
            new Dictionary<string, object> { ["score"] = "10" }));
        Assert.False(_engine.EvaluateCondition(
            condition,
            new Dictionary<string, object> { ["score"] = "20" }));
    }

    [Fact]
    public void EvaluateCondition_HasLength_ReturnsFalseForCamelCaseOperator()
    {
        var condition = SimpleCondition("code", ConditionalLogicConstants.Operators.HasLength, "5");

        Assert.False(_engine.EvaluateCondition(
            condition,
            new Dictionary<string, object> { ["code"] = "abcde" }));
    }

    [Fact]
    public void EvaluateCondition_MatchesPattern_ReturnsFalseForCamelCaseOperator()
    {
        var condition = SimpleCondition("ref", ConditionalLogicConstants.Operators.MatchesPattern, "^REF-\\d+$");

        Assert.False(_engine.EvaluateCondition(
            condition,
            new Dictionary<string, object> { ["ref"] = "REF-123" }));
    }

    [Fact]
    public void EvaluateCondition_IsValidEmail_ReturnsFalseForCamelCaseOperator()
    {
        var condition = SimpleCondition("email", ConditionalLogicConstants.Operators.IsValidEmail, string.Empty);

        Assert.False(_engine.EvaluateCondition(
            condition,
            new Dictionary<string, object> { ["email"] = "user@example.com" }));
    }

    [Fact]
    public void EvaluateCondition_IsValidPhone_ReturnsFalseForCamelCaseOperator()
    {
        var condition = SimpleCondition("phone", ConditionalLogicConstants.Operators.IsValidPhone, string.Empty);

        Assert.False(_engine.EvaluateCondition(
            condition,
            new Dictionary<string, object> { ["phone"] = "7123456789" }));
    }

    [Fact]
    public void EvaluateCondition_MissingField_TreatedAsNull_ForEquals()
    {
        var condition = SimpleCondition("missing", ConditionalLogicConstants.Operators.Equals, string.Empty);
        Assert.True(_engine.EvaluateCondition(condition, []));
    }

    [Fact]
    public void EvaluateCondition_UnsupportedOperator_ReturnsFalse()
    {
        var condition = SimpleCondition("field", "unknownOperator", "x");

        Assert.False(_engine.EvaluateCondition(condition, new Dictionary<string, object> { ["field"] = "x" }));
    }

    [Fact]
    public void EvaluateConditionGroup_And_RequiresAllConditions()
    {
        var group = new ConditionGroup
        {
            LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
            Conditions =
            [
                SimpleCondition("a", ConditionalLogicConstants.Operators.Equals, "yes"),
                SimpleCondition("b", ConditionalLogicConstants.Operators.Equals, "filled")
            ]
        };

        Assert.True(_engine.EvaluateConditionGroup(
            group,
            new Dictionary<string, object> { ["a"] = "yes", ["b"] = "filled" }));
        Assert.False(_engine.EvaluateConditionGroup(
            group,
            new Dictionary<string, object> { ["a"] = "yes", ["b"] = "" }));
    }

    [Fact]
    public void EvaluateConditionGroup_Or_RequiresAnyCondition()
    {
        var group = new ConditionGroup
        {
            LogicalOperator = ConditionalLogicConstants.LogicalOperators.Or,
            Conditions =
            [
                SimpleCondition("a", ConditionalLogicConstants.Operators.Equals, "yes"),
                SimpleCondition("b", ConditionalLogicConstants.Operators.Equals, "yes")
            ]
        };

        Assert.True(_engine.EvaluateConditionGroup(
            group,
            new Dictionary<string, object> { ["a"] = "no", ["b"] = "yes" }));
        Assert.False(_engine.EvaluateConditionGroup(
            group,
            new Dictionary<string, object> { ["a"] = "no", ["b"] = "no" }));
    }

    [Fact]
    public void EvaluateConditionGroup_Not_InvertsAllResults()
    {
        var group = new ConditionGroup
        {
            LogicalOperator = ConditionalLogicConstants.LogicalOperators.Not,
            Conditions =
            [
                SimpleCondition("a", ConditionalLogicConstants.Operators.Equals, "yes"),
                SimpleCondition("b", ConditionalLogicConstants.Operators.Equals, "yes")
            ]
        };

        Assert.False(_engine.EvaluateConditionGroup(
            group,
            new Dictionary<string, object> { ["a"] = "yes", ["b"] = "yes" }));
        Assert.True(_engine.EvaluateConditionGroup(
            group,
            new Dictionary<string, object> { ["a"] = "yes", ["b"] = "no" }));
    }

    [Fact]
    public void EvaluateConditionGroup_EmptyConditions_ReturnsTrue()
    {
        var group = new ConditionGroup
        {
            LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
            Conditions = []
        };

        Assert.True(_engine.EvaluateConditionGroup(group, []));
    }

    [Fact]
    public void EvaluateConditionGroup_NestedConditions()
    {
        var group = new ConditionGroup
        {
            LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
            Conditions =
            [
                new Condition
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.Or,
                    Conditions =
                    [
                        SimpleCondition("a", ConditionalLogicConstants.Operators.Equals, "1"),
                        SimpleCondition("b", ConditionalLogicConstants.Operators.Equals, "2")
                    ],
                    TriggerField = "",
                    Operator = "",
                    Value = ""
                },
                SimpleCondition("c", ConditionalLogicConstants.Operators.Equals, "ok")
            ]
        };

        Assert.True(_engine.EvaluateConditionGroup(
            group,
            new Dictionary<string, object> { ["a"] = "1", ["c"] = "ok" }));
        Assert.True(_engine.EvaluateConditionGroup(
            group,
            new Dictionary<string, object> { ["b"] = "2", ["c"] = "ok" }));
        Assert.False(_engine.EvaluateConditionGroup(
            group,
            new Dictionary<string, object> { ["a"] = "x", ["b"] = "x", ["c"] = "ok" }));
    }

    [Fact]
    public void GetTriggeredRules_ShouldReturnRulesReferencingField()
    {
        var rules = new[]
        {
            Rule(
                "triggered",
                new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions =
                    [
                        SimpleCondition(
                            "amount",
                            ConditionalLogicConstants.Operators.GreaterThan,
                            10,
                            ConditionalLogicConstants.DataTypes.Number)
                    ]
                }),
            Rule(
                "other",
                new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions =
                    [
                        SimpleCondition("note", ConditionalLogicConstants.Operators.IsNotEmpty, "")
                    ]
                },
                [
                    new AffectedElement
                    {
                        ElementId = "note-page",
                        ElementType = ConditionalLogicConstants.ElementTypes.Page,
                        Action = ConditionalLogicConstants.Actions.Hide
                    }
                ])
        };

        var triggered = _engine.GetTriggeredRules(rules, "amount").ToList();

        Assert.Single(triggered);
        Assert.Equal("triggered", triggered[0].Id);
    }

    [Fact]
    public void GetTriggeredRules_ShouldFindFieldInNestedConditions()
    {
        var rules = new[]
        {
            Rule(
                "nested",
                new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions =
                    [
                        new Condition
                        {
                            LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                            Conditions =
                            [
                                new Condition
                        {
                            TriggerField = "inner",
                            Operator = ConditionalLogicConstants.Operators.Equals,
                            Value = "filled",
                            DataType = ConditionalLogicConstants.DataTypes.String
                        }
                            ],
                            TriggerField = "",
                            Operator = "",
                            Value = ""
                        }
                    ]
                })
        };

        var triggered = _engine.GetTriggeredRules(rules, "inner").ToList();

        Assert.Single(triggered);
    }

    [Fact]
    public void GetTriggeredRules_ShouldExcludeDisabledRules()
    {
        var rules = new[]
        {
            Rule(
                "disabled",
                new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions = [SimpleCondition("field", ConditionalLogicConstants.Operators.IsNotEmpty, "")]
                },
                enabled: false)
        };

        Assert.Empty(_engine.GetTriggeredRules(rules, "field"));
    }

    [Fact]
    public void ValidateRule_ShouldReportMissingRequiredFields()
    {
        var result = _engine.ValidateRule(new ConditionalLogic
        {
            Id = "",
            ConditionGroup = null!,
            AffectedElements = []
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Rule ID is required", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.Contains("Condition group is required", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.Contains("At least one affected element is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRule_ShouldReportInvalidConditionAndElement()
    {
        var result = _engine.ValidateRule(new ConditionalLogic
        {
            Id = "rule-1",
            ConditionGroup = new ConditionGroup
            {
                LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                Conditions =
                [
                    new Condition
                    {
                        TriggerField = "",
                        Operator = "",
                        Value = ""
                    }
                ]
            },
            AffectedElements =
            [
                new AffectedElement
                {
                    ElementId = "",
                    ElementType = "",
                    Action = ""
                }
            ]
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Condition trigger field is required", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.Contains("Affected element ID is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRule_ShouldWarnWhenConditionGroupHasNoConditions()
    {
        var result = _engine.ValidateRule(new ConditionalLogic
        {
            Id = "rule-1",
            ConditionGroup = new ConditionGroup
            {
                LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                Conditions = []
            },
            AffectedElements =
            [
                new AffectedElement
                {
                    ElementId = "f1",
                    ElementType = ConditionalLogicConstants.ElementTypes.Field,
                    Action = ConditionalLogicConstants.Actions.Show
                }
            ]
        });

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("Condition group has no conditions", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRule_ShouldValidateNestedConditions()
    {
        var result = _engine.ValidateRule(new ConditionalLogic
        {
            Id = "rule-1",
            ConditionGroup = new ConditionGroup
            {
                LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                Conditions =
                [
                    new Condition
                    {
                        LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                        Conditions =
                        [
                            new Condition
                            {
                                TriggerField = "",
                                Operator = ConditionalLogicConstants.Operators.Equals,
                                Value = "x"
                            }
                        ],
                        TriggerField = "",
                        Operator = "",
                        Value = ""
                    }
                ]
            },
            AffectedElements =
            [
                new AffectedElement
                {
                    ElementId = "f1",
                    ElementType = ConditionalLogicConstants.ElementTypes.Field,
                    Action = ConditionalLogicConstants.Actions.Show
                }
            ]
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Condition trigger field is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRule_ShouldPassForWellFormedRule()
    {
        var result = _engine.ValidateRule(new ConditionalLogic
        {
            Id = "rule-1",
            ConditionGroup = new ConditionGroup
            {
                LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                Conditions =
                [
                    SimpleCondition("field", ConditionalLogicConstants.Operators.Equals, "yes")
                ]
            },
            AffectedElements =
            [
                new AffectedElement
                {
                    ElementId = "f1",
                    ElementType = ConditionalLogicConstants.ElementTypes.Field,
                    Action = ConditionalLogicConstants.Actions.Show
                }
            ]
        });

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    private static AffectedElement Affected(string elementId) =>
        new()
        {
            ElementId = elementId,
            ElementType = ConditionalLogicConstants.ElementTypes.Field,
            Action = ConditionalLogicConstants.Actions.Show
        };
}
