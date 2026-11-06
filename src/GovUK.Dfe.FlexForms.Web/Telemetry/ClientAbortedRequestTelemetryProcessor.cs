using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Records 499 (client closed the request, e.g. navigated away mid-fetch) as a successful request.
/// Application Insights treats every response code of 400 or above as a failure, which put aborted
/// <c>/notifications/unread</c> polls in the Failures blade. The 499 response code is kept.
/// </summary>
public class ClientAbortedRequestTelemetryProcessor(ITelemetryProcessor next) : ITelemetryProcessor
{
    private const string ClientClosedRequest = "499";

    public void Process(ITelemetry item)
    {
        if (item is RequestTelemetry { ResponseCode: ClientClosedRequest } request)
            request.Success = true;

        next.Process(item);
    }
}
