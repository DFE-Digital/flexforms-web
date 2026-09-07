using GovUK.Dfe.FlexForms.Web.Telemetry;
using Serilog.Events;
using Serilog.Parsing;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Telemetry;

public class PiiMaskingTests
{
    [Fact]
    public void MaskEmail_ShouldKeepFirstTwoAndLastFiveCharacters()
    {
        var masked = PiiMasking.MaskEmail("farshad.dashti@education.gov.uk");

        Assert.Equal("fa************************ov.uk", masked);
        Assert.StartsWith("fa", masked);
        Assert.EndsWith("ov.uk", masked);
    }

    [Fact]
    public void MaskEmail_ShouldHandleShortValues()
    {
        Assert.Equal("ab*****", PiiMasking.MaskEmail("ab@x.co"));
    }

    [Fact]
    public void MaskEmailsInText_ShouldMaskEmbeddedAddresses()
    {
        var text = "Sent to farshad.dashti@education.gov.uk successfully";
        var masked = PiiMasking.MaskEmailsInText(text);

        Assert.DoesNotContain("farshad.dashti@education.gov.uk", masked);
        Assert.Contains("fa", masked);
        Assert.Contains("ov.uk", masked);
    }
}

public class PiiMaskingTextFormatterTests
{
    private const string FullEmail = "farshad.dashti@education.gov.uk";

    [Fact]
    public void Format_ShouldMaskEmailInMessage()
    {
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse($"UserEmail={FullEmail}"),
            []);

        using var writer = new StringWriter();
        new PiiMaskingTextFormatter().Format(logEvent, writer);
        var output = writer.ToString();

        Assert.DoesNotContain(FullEmail, output);
        Assert.Contains("fa", output);
        Assert.Contains("ov.uk", output);
    }
}
