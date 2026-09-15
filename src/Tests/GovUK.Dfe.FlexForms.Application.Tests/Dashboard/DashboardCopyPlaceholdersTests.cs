using GovUK.Dfe.FlexForms.Application.Dashboard;

namespace GovUK.Dfe.FlexForms.Application.Tests.Dashboard;

public class DashboardCopyPlaceholdersTests
{
    [Theory]
    [InlineData("Your applications for {template_name}", "Your applications for Transfer")]
    [InlineData("Your applications for {TEMPLATE_NAME}", "Your applications for Transfer")]
    [InlineData("{template_name} applications in progress", "Transfer applications in progress")]
    public void Apply_ShouldSubstituteTemplateName(string copy, string expected) =>
        Assert.Equal(expected, DashboardCopyPlaceholders.Apply(copy, "Transfer"));

    [Fact]
    public void Apply_ShouldLeaveCopyUnchanged_WhenThereIsNoPlaceholder() =>
        Assert.Equal("Your applications", DashboardCopyPlaceholders.Apply("Your applications", "Transfer"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Apply_ShouldTidySpacing_WhenTemplateNameIsUnknown(string? templateName) =>
        Assert.Equal(
            "Your applications for",
            DashboardCopyPlaceholders.Apply("Your applications for {template_name}", templateName));

    [Fact]
    public void Apply_ShouldNotLeaveADoubleSpace_WhenPlaceholderIsMidSentence() =>
        Assert.Equal(
            "Start a new application",
            DashboardCopyPlaceholders.Apply("Start a new {template_name} application", null));
}
