using GovUK.Dfe.FlexForms.Application.DisplayCopy;

namespace GovUK.Dfe.FlexForms.Application.Tests.DisplayCopy;

public class DisplayCopyPlaceholdersTests
{
    [Fact]
    public void Apply_ReplacesAllTokens_CaseInsensitively()
    {
        var result = DisplayCopyPlaceholders.Apply(
            "Start a {SINGULAR} for {Template_Name} ({PLURAL}) — e.g. {Contributor_Email}",
            "School visits",
            "visit",
            "visits",
            "name@school.sch.uk");

        Assert.Equal(
            "Start a visit for School visits (visits) — e.g. name@school.sch.uk",
            result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Apply_ReturnsEmptyCopy_WhenCopyIsBlank(string? copy)
    {
        Assert.Equal(copy, DisplayCopyPlaceholders.Apply(copy!, "t", "s", "p", "e"));
    }

    [Fact]
    public void Apply_LeavesUnknownTokensUnchanged()
    {
        var result = DisplayCopyPlaceholders.Apply(
            "Hello {unknown}",
            null,
            "visit",
            "visits",
            null);

        Assert.Equal("Hello {unknown}", result);
    }
}
