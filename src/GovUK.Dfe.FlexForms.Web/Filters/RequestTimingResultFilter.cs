using GovUK.Dfe.FlexForms.Application.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GovUK.Dfe.FlexForms.Web.Filters;

/// <summary>
/// Adds the time taken to produce a page's or action's result (rendering the Razor view, or writing a redirect)
/// to the request's timings as "render page".
/// </summary>
public sealed class RequestTimingResultFilter : IAsyncAlwaysRunResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (RequestTimings.Current is null)
        {
            await next();
            return;
        }

        using (RequestTimings.Measure("render page"))
            await next();
    }
}
