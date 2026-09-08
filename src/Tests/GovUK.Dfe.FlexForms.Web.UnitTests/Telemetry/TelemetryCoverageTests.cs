using System.Globalization;
using GovUK.Dfe.CoreLibs.Http.Logging;
using GovUK.Dfe.FlexForms.Web.Telemetry;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Serilog.Events;
using Serilog.Parsing;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Telemetry;

public class HealthCheckTelemetryFilterTests
{
    [Fact]
    public void Process_ShouldDropHealthRequests_AndForwardOthers()
    {
        var next = new RecordingProcessor();
        var filter = new HealthCheckTelemetryFilter(next);

        filter.Process(new RequestTelemetry { Url = new Uri("https://app/health") });
        filter.Process(new RequestTelemetry { Url = new Uri("https://app/HEALTHZ") });
        filter.Process(new RequestTelemetry { Url = new Uri("https://app/applications") });
        filter.Process(new TraceTelemetry("keep"));

        Assert.Equal(2, next.Items.Count);
        Assert.Contains(next.Items, i => i is RequestTelemetry);
        Assert.Contains(next.Items, i => i is TraceTelemetry);
    }

    private sealed class RecordingProcessor : ITelemetryProcessor
    {
        public List<ITelemetry> Items { get; } = [];
        public void Process(ITelemetry item) => Items.Add(item);
    }
}

public class FlexFormsRequestScopeTests
{
    [Fact]
    public void ToScopeDictionary_ShouldOmitBlankValues()
    {
        var scope = new FlexFormsRequestScope
        {
            TemplateId = "tpl",
            ApplicationId = " ",
            ApplicationReference = "REF-1"
        };

        var dict = scope.ToScopeDictionary();
        Assert.Equal("tpl", dict[FlexFormsLogContextKeys.TemplateId]);
        Assert.Equal("REF-1", dict[FlexFormsLogContextKeys.ApplicationReference]);
        Assert.False(dict.ContainsKey(FlexFormsLogContextKeys.ApplicationId));
    }
}

public class TenantApplicationInsightsScopeTests
{
    [Fact]
    public void Begin_ShouldSetAndRestoreConnectionString()
    {
        using (TenantApplicationInsightsScope.Begin(" InstrumentationKey=abc "))
        {
            Assert.Equal("InstrumentationKey=abc", TenantApplicationInsightsScope.CurrentConnectionString);
            using (TenantApplicationInsightsScope.Begin(" "))
            {
                Assert.Null(TenantApplicationInsightsScope.CurrentConnectionString);
            }
            Assert.Equal("InstrumentationKey=abc", TenantApplicationInsightsScope.CurrentConnectionString);
        }
        Assert.Null(TenantApplicationInsightsScope.CurrentConnectionString);
    }
}

public class ExceptionTrackingTelemetryConverterTests
{
    [Fact]
    public void Convert_ShouldMaskEmailAndCopyStructuredProperties()
    {
        var converter = new ExceptionTrackingTelemetryConverter();
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Error,
            new InvalidOperationException("boom"),
            new MessageTemplateParser().Parse("Failed for {UserEmail} ErrorId={ErrorId}"),
            [
                new LogEventProperty("UserEmail", new ScalarValue("ada@education.gov.uk")),
                new LogEventProperty(LogContextKeys.ErrorId, new ScalarValue("ERR-1")),
                new LogEventProperty(FlexFormsLogContextKeys.TemplateId, new ScalarValue("tpl-1"))
            ]);

        var items = converter.Convert(logEvent, CultureInfo.InvariantCulture).ToList();

        var exception = Assert.IsType<ExceptionTelemetry>(items[0]);
        Assert.Equal("ERR-1", exception.Properties[LogContextKeys.ErrorId]);
        Assert.Equal("tpl-1", exception.Properties[FlexFormsLogContextKeys.TemplateId]);
        Assert.DoesNotContain("ada@education.gov.uk", exception.Properties["LogMessage"]);
        Assert.Contains("ad", exception.Properties["UserEmail"]);
    }

    [Fact]
    public void Convert_ShouldApplyRegexFallbacksOnTrace()
    {
        var converter = new ExceptionTrackingTelemetryConverter();
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Warning,
            exception: null,
            new MessageTemplateParser().Parse("ErrorId=ABC-123 CorrelationId=aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee done"),
            []);

        var items = converter.Convert(logEvent, CultureInfo.InvariantCulture).ToList();
        var trace = Assert.IsType<TraceTelemetry>(items[0]);
        Assert.Equal("ABC-123", trace.Properties[LogContextKeys.ErrorId]);
        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", trace.Properties[LogContextKeys.CorrelationId]);
    }
}
