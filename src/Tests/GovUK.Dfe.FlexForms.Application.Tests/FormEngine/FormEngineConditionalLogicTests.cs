using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class FormEngineConditionalLogicTests
{
    [Fact]
    public async Task ApplyAsync_ShouldReturnEmpty_WhenNoRules()
    {
        var state = await FormEngineConditionalLogic.ApplyAsync(
            FormEngineConstants.CreateDummyTemplate(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            Substitute.For<IConditionalLogicOrchestrator>(),
            "p1",
            "t1",
            "load",
            NullLogger.Instance);

        Assert.Empty(state.FieldValues);
    }

    [Fact]
    public async Task ApplyAsync_ShouldCopyFieldValuesIntoData()
    {
        var orchestrator = Substitute.For<IConditionalLogicOrchestrator>();
        orchestrator.ApplyConditionalLogicAsync(default!, default!, default)
            .ReturnsForAnyArgs(new FormConditionalState { FieldValues = { ["name"] = "Ada" } });

        var template = FormEngineConstants.CreateDummyTemplate();
        template.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Enabled = true,
                ConditionGroup = new ConditionGroup { LogicalOperator = "AND", Conditions = [] },
                AffectedElements = []
            }
        ];
        var data = new Dictionary<string, object>();

        var state = await FormEngineConditionalLogic.ApplyAsync(
            template,
            data,
            new Dictionary<string, object> { ["other"] = "x" },
            orchestrator,
            "p1",
            "t1",
            "change",
            NullLogger.Instance,
            new Dictionary<string, object> { ["extra"] = "y" });

        Assert.Equal("Ada", data["name"]);
        Assert.Equal("Ada", state.FieldValues["name"]);
    }

    [Fact]
    public async Task ApplyAsync_ShouldReturnEmpty_WhenOrchestratorThrows()
    {
        var orchestrator = Substitute.For<IConditionalLogicOrchestrator>();
        orchestrator.ApplyConditionalLogicAsync(
                Arg.Any<FormTemplate>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<ConditionalLogicContext?>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var template = FormEngineConstants.CreateDummyTemplate();
        template.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Enabled = true,
                ConditionGroup = new ConditionGroup { LogicalOperator = "AND", Conditions = [] },
                AffectedElements = []
            }
        ];

        var state = await FormEngineConditionalLogic.ApplyAsync(
            template,
            new Dictionary<string, object> { ["name"] = "x" },
            new Dictionary<string, object>(),
            orchestrator,
            "p1",
            "t1",
            "load",
            NullLogger.Instance);

        Assert.Empty(state.FieldValues);
    }
}
