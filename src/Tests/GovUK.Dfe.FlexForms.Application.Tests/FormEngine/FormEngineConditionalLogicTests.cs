using GovUK.Dfe.FlexForms.Application.FormEngine;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class FormEngineConditionalLogicTests
{
    [Fact]
    public void BuildEvaluationData_ShouldMergeAccumulatedAnswersMissingFromCurrentPage()
    {
        var result = FormEngineConditionalLogic.BuildEvaluationData(
            new Dictionary<string, object> { ["containsSenTest"] = "answer" },
            new Dictionary<string, object>(),
            new Dictionary<string, object>
            {
                ["significantChangeType"] = new[] { "sen", "senUnit" }
            });

        Assert.Equal("answer", result["containsSenTest"]);
        Assert.Equal(new[] { "sen", "senUnit" }, result["significantChangeType"]);
    }
}
