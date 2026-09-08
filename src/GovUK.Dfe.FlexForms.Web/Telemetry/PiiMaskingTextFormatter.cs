using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Console formatter that masks email addresses in the rendered log line.
/// Azure Container Apps log stream is stdout from Serilog's Console sink;
/// Application Insights converters do not apply to that path.
/// </summary>
public sealed class PiiMaskingTextFormatter : ITextFormatter
{
    internal const string DefaultOutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    private readonly ITextFormatter _inner;

    public PiiMaskingTextFormatter(ITextFormatter? inner = null)
    {
        _inner = inner ?? new MessageTemplateTextFormatter(DefaultOutputTemplate);
    }

    public void Format(LogEvent logEvent, TextWriter output)
    {
        using var buffer = new StringWriter();
        _inner.Format(logEvent, buffer);
        output.Write(PiiMasking.MaskEmailsInText(buffer.ToString()));
    }
}
