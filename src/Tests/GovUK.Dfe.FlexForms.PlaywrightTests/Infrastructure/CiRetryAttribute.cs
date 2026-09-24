using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using Microsoft.Playwright;
using NUnit.Framework;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;

/// <summary>
/// Mirrors playwright.config.ts's `retries: process.env.CI ? 2 : 0` (2 retries = 3 total
/// attempts, 1 = no retry locally). NUnit's [Retry] only retries assertion failures by default,
/// so the exception types below - verified against Microsoft.Playwright.dll 1.61.0 (action/
/// navigation timeouts throw System.TimeoutException, Expect(...) failures throw
/// Microsoft.Playwright.PlaywrightException, which also covers the internal TargetClosedException
/// subclass our timeout watchdog raises) plus our own ApiRequestException - are listed explicitly.
/// </summary>
public sealed class CiRetryAttribute : RetryAttribute
{
    public CiRetryAttribute()
        : base(IsCi ? 3 : 1)
    {
        RetryExceptions = [typeof(PlaywrightException), typeof(TimeoutException), typeof(ApiRequestException)];
    }

    private static bool IsCi => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));
}
