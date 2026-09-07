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
    [Fact]
    public async Task ApplyConditionalLogicAsync_ShouldReturnDefaultState_WhenNoRules()
    {
        var engine = Substitute.For<IConditionalLogicEngine>();
        var orchestrator = new ConditionalLogicOrchestrator(engine, NullLogger<ConditionalLogicOrchestrator>.Instance);
        var template = FormEngineConstants.CreateDummyTemplate();

        var state = await orchestrator.ApplyConditionalLogicAsync(template, []);

        Assert.NotNull(state);
        Assert.Empty(state.FieldVisibility);
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

        var orchestrator = new ConditionalLogicOrchestrator(engine, NullLogger<ConditionalLogicOrchestrator>.Instance);
        var template = FormEngineConstants.CreateDummyTemplate();
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

        var orchestrator = new ConditionalLogicOrchestrator(engine, NullLogger<ConditionalLogicOrchestrator>.Instance);
        var template = FormEngineConstants.CreateDummyTemplate();
        template.ConditionalLogic = triggered.ToList();

        var result = await orchestrator.EvaluateFieldChangeAsync(template, [], "amount");

        Assert.Equal(["on-change"], result.EvaluatedRules);
        engine.Received(1).GetTriggeredRules(template.ConditionalLogic, "amount");
    }

    [Fact]
    public async Task ValidateTemplateRulesAsync_ShouldReturnValidationResults()
    {
        var engine = Substitute.For<IConditionalLogicEngine>();
        engine.ValidateRule(Arg.Any<ConditionalLogic>())
            .Returns(new ConditionalLogicValidationResult { IsValid = true });

        var orchestrator = new ConditionalLogicOrchestrator(engine, NullLogger<ConditionalLogicOrchestrator>.Instance);
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
}
