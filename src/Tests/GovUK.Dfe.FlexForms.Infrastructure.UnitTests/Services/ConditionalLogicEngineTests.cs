using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class ConditionalLogicEngineTests
{
    private readonly ConditionalLogicEngine _engine =
        new(NullLogger<ConditionalLogicEngine>.Instance);

    [Fact]
    public void EvaluateRules_ShouldAddActions_WhenConditionMatches()
    {
        var rules = new[]
        {
            new ConditionalLogic
            {
                Id = "show-name",
                Enabled = true,
                Priority = 1,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions =
                    [
                        new Condition
                        {
                            TriggerField = "kind",
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
                        ElementId = "details",
                        ElementType = ConditionalLogicConstants.ElementTypes.Field,
                        Action = ConditionalLogicConstants.Actions.Show
                    }
                ]
            }
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
            new ConditionalLogic
            {
                Id = "disabled",
                Enabled = false,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions = []
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "hidden",
                        ElementType = ConditionalLogicConstants.ElementTypes.Field,
                        Action = ConditionalLogicConstants.Actions.Show
                    }
                ]
            }
        };

        var result = _engine.EvaluateRules(rules, []);

        Assert.Empty(result.EvaluatedRules);
        Assert.Empty(result.Actions);
    }

    [Fact]
    public void GetTriggeredRules_ShouldReturnRulesReferencingField()
    {
        var rules = new[]
        {
            new ConditionalLogic
            {
                Id = "triggered",
                Enabled = true,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions =
                    [
                        new Condition
                        {
                            TriggerField = "amount",
                            Operator = ConditionalLogicConstants.Operators.GreaterThan,
                            Value = 10,
                            DataType = ConditionalLogicConstants.DataTypes.Number
                        }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "extra",
                        ElementType = ConditionalLogicConstants.ElementTypes.Field,
                        Action = ConditionalLogicConstants.Actions.Show
                    }
                ]
            },
            new ConditionalLogic
            {
                Id = "other",
                Enabled = true,
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = ConditionalLogicConstants.LogicalOperators.And,
                    Conditions =
                    [
                        new Condition
                        {
                            TriggerField = "note",
                            Operator = ConditionalLogicConstants.Operators.IsNotEmpty,
                            Value = string.Empty,
                            DataType = ConditionalLogicConstants.DataTypes.String
                        }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement
                    {
                        ElementId = "note-page",
                        ElementType = ConditionalLogicConstants.ElementTypes.Page,
                        Action = ConditionalLogicConstants.Actions.Hide
                    }
                ]
            }
        };

        var triggered = _engine.GetTriggeredRules(rules, "amount").ToList();

        Assert.Single(triggered);
        Assert.Equal("triggered", triggered[0].Id);
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
    }
}
