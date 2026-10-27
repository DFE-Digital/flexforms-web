using System.Text.RegularExpressions;
using GovUK.Dfe.FlexForms.Application.Diagnostics;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Adds each outgoing HTTP call to the request's timings as "api METHOD /path", with ids in the path replaced by {id}
/// so calls to the same endpoint are added together.
/// </summary>
public sealed partial class RequestTimingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (RequestTimings.Current is null)
            return await base.SendAsync(request, cancellationToken);

        using (RequestTimings.Measure($"api {request.Method.Method} {StepPath(request.RequestUri)}"))
            return await base.SendAsync(request, cancellationToken);
    }

    public static string StepPath(Uri? uri)
    {
        if (uri is null)
            return "(no address)";

        var path = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString.Split('?')[0];
        return string.Join('/', path.Split('/').Select(segment =>
            IdSegment().IsMatch(segment) && !ApiVersionSegment().IsMatch(segment) ? "{id}" : segment));
    }

    // Guids, numbers, references like TRF-20261026-004 and version numbers: anything with a digit in it.
    [GeneratedRegex(@"\d")]
    private static partial Regex IdSegment();

    [GeneratedRegex(@"^v\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex ApiVersionSegment();
}
