using GovUK.Dfe.FlexForms.Domain.Templates;

namespace GovUK.Dfe.FlexForms.Domain.Tests.Templates;

public class TemplateVersionPolicyTests
{
    [Theory]
    [InlineData(null, "1.0.1")]
    [InlineData("", "1.0.1")]
    [InlineData("1", "1.0.1")]
    [InlineData("1.0", "1.0.1")]
    [InlineData("1.0.1", "1.0.2")]
    [InlineData("2.3.9", "2.3.10")]
    [InlineData("1.0.x", "1.0.1")]
    public void IncrementPatch_ShouldMatchPreviousPageModelRules(string? version, string expected)
    {
        Assert.Equal(expected, TemplateVersionPolicy.IncrementPatch(version));
    }
}
