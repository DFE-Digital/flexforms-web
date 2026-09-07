using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Notifications;
using GovUK.Dfe.FlexForms.Application.Options;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class AdminPageOutcomeTests
{
    [Fact]
    public void Factories_ShouldSetKind()
    {
        Assert.Equal(AdminPageOutcomeKind.StayOnPage, AdminPageOutcome.Stay(errorMessage: "e").Kind);
        Assert.Equal("ok", AdminPageOutcome.Redirect(successMessage: "ok").SuccessMessage);
        var file = AdminPageOutcome.File([1, 2], "text/plain", "a.txt");
        Assert.Equal(AdminPageOutcomeKind.FileDownload, file.Kind);
        Assert.Equal("a.txt", file.FileDownloadName);
    }
}

public class AdminSettingsEncodingTests
{
    [Fact]
    public void ToBase64_ShouldRoundTripUtf8()
    {
        var encoded = AdminSettingsEncoding.ToBase64("""{"a":1}""");
        Assert.Equal("""{"a":1}""", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
    }
}

public class NotificationScopeContextTests
{
    [Fact]
    public void PrefixDetail_ShouldJoinWhenDetailPresent()
    {
        Assert.Equal("Transfers", NotificationScopeContext.PrefixDetail(" Transfers ", null));
        Assert.Equal("Transfers|file-upload", NotificationScopeContext.PrefixDetail("Transfers", " file-upload "));
    }

    [Fact]
    public void Build_ShouldSkipBlankParts()
    {
        Assert.Equal("Transfers|file-upload|abc", NotificationScopeContext.Build("Transfers", "file-upload", " ", "abc"));
        Assert.Throws<ArgumentException>(() => NotificationScopeContext.Build(" "));
    }
}

public class ApplicationOptionsTests
{
    [Fact]
    public void Defaults_ShouldMatchExpectedValues()
    {
        Assert.Equal(50, new DashboardOptions().PageSize);
        Assert.Equal("Important", new NotificationBannerOptions().Heading);
        Assert.False(new NotificationBannerOptions().Enabled);
        Assert.Equal("application", new ApplicationTerminologyOptions().Singular);
        Assert.Equal("applications", new ApplicationTerminologyOptions().Plural);
        Assert.Equal("1.0", new SchemaEventDefinitionOptions().Version);
        Assert.Equal(string.Empty, new SchemaEventDefinitionOptions().TopicName);
    }
}
