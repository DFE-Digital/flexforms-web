using System.Text.RegularExpressions;
using GovUK.Dfe.CoreLibs.Http.Logging;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Ensures Serilog structured properties become searchable App Insights customDimensions.
/// </summary>
public sealed class ExceptionTrackingTelemetryConverter : TraceTelemetryConverter
{
    private static readonly string[] StructuredTelemetryKeys =
    [
        LogContextKeys.ErrorId,
        LogContextKeys.CorrelationId,
        LogContextKeys.TenantId,
        LogContextKeys.TenantName,
        LogContextKeys.UserEmail,
        LogContextKeys.ServiceName,
        FlexFormsLogContextKeys.TemplateId,
        FlexFormsLogContextKeys.ApplicationId,
        FlexFormsLogContextKeys.ApplicationReference
    ];

    private static readonly Regex ErrorIdPattern = new(@"ErrorId[:\s=]+([A-Za-z0-9\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CorrelationIdPattern = new(@"CorrelationId[:\s=]+([a-f0-9\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override IEnumerable<ITelemetry> Convert(LogEvent logEvent, IFormatProvider formatProvider)
    {
        if (logEvent.Exception != null)
        {
            var exceptionTelemetry = new ExceptionTelemetry(logEvent.Exception)
            {
                SeverityLevel = ConvertSeverityLevel(logEvent.Level),
                Timestamp = logEvent.Timestamp
            };

            var renderedMessage = logEvent.RenderMessage(formatProvider);
            exceptionTelemetry.Properties["LogMessage"] = renderedMessage;

            ApplyStructuredProperties(exceptionTelemetry.Properties, logEvent);
            ApplyRegexFallbacks(exceptionTelemetry.Properties, renderedMessage);

            foreach (var property in logEvent.Properties)
            {
                var value = FormatPropertyValue(property.Value?.ToString());
                if (!string.IsNullOrEmpty(value) && !exceptionTelemetry.Properties.ContainsKey(property.Key))
                    exceptionTelemetry.Properties[property.Key] = value;
            }

            yield return exceptionTelemetry;

            foreach (var trace in base.Convert(logEvent, formatProvider))
                yield return trace;
        }
        else
        {
            foreach (var telemetry in base.Convert(logEvent, formatProvider))
            {
                if (telemetry is TraceTelemetry traceTelemetry)
                {
                    ApplyStructuredProperties(traceTelemetry.Properties, logEvent);
                    ApplyRegexFallbacks(traceTelemetry.Properties, logEvent.RenderMessage(formatProvider));
                }

                yield return telemetry;
            }
        }
    }

    private static void ApplyStructuredProperties(IDictionary<string, string> target, LogEvent logEvent)
    {
        foreach (var key in StructuredTelemetryKeys)
        {
            if (target.ContainsKey(key))
                continue;

            if (TryGetPropertyValue(logEvent, key, out var value))
                target[key] = value;
        }
    }

    private static void ApplyRegexFallbacks(IDictionary<string, string> target, string renderedMessage)
    {
        if (!target.ContainsKey(LogContextKeys.ErrorId))
        {
            var errorIdMatch = ErrorIdPattern.Match(renderedMessage);
            if (errorIdMatch.Success)
                target[LogContextKeys.ErrorId] = errorIdMatch.Groups[1].Value;
        }

        if (!target.ContainsKey(LogContextKeys.CorrelationId))
        {
            var correlationIdMatch = CorrelationIdPattern.Match(renderedMessage);
            if (correlationIdMatch.Success)
                target[LogContextKeys.CorrelationId] = correlationIdMatch.Groups[1].Value;
        }
    }

    private static bool TryGetPropertyValue(LogEvent logEvent, string key, out string value)
    {
        value = string.Empty;
        if (!logEvent.Properties.TryGetValue(key, out var propertyValue))
            return false;

        var formatted = FormatPropertyValue(propertyValue.ToString());
        if (string.IsNullOrWhiteSpace(formatted))
            return false;

        value = formatted;
        return true;
    }

    private static string? FormatPropertyValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
            return value[1..^1];

        return value;
    }

    private static SeverityLevel ConvertSeverityLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => SeverityLevel.Verbose,
        LogEventLevel.Debug => SeverityLevel.Verbose,
        LogEventLevel.Information => SeverityLevel.Information,
        LogEventLevel.Warning => SeverityLevel.Warning,
        LogEventLevel.Error => SeverityLevel.Error,
        LogEventLevel.Fatal => SeverityLevel.Critical,
        _ => SeverityLevel.Information
    };
}
