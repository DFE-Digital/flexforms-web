using System.Threading;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;

/// <summary>
/// Shared base for every test fixture, replacing fixtures/test.ts and playwright.config.ts's
/// `use` block. ApiConfig/ApiClient/AdminApiClient/Terminology were worker-scoped fixtures in
/// the TS suite (one instance per Node worker process); here they are fixture-scoped via
/// [OneTimeSetUp], which is the closest equivalent given that NUnit runs many fixtures
/// concurrently within one process rather than one test file per worker.
/// </summary>
/// <remarks>
/// The [Category] here is inherited by every derived fixture. This project is part of
/// GovUK.Dfe.FlexForms.Web.sln (for IDE convenience only); dotnet-ci.yml's solution-wide
/// `dotnet test` excludes TestCategory=Playwright so it doesn't try to launch a browser there.
/// </remarks>
[Category("Playwright")]
public abstract class PlaywrightTestBase : PageTest
{
    private string _artifactsDirectory = null!;
    private Timer? _timeoutWatchdog;

    protected ApiConfig ApiConfig { get; private set; } = null!;

    protected ServiceConfig ServiceConfig { get; private set; } = null!;

    protected Terminology Terminology => ServiceConfig.Terminology;

    protected IAPIRequestContext ApiClient { get; private set; } = null!;

    protected IAPIRequestContext AdminApiClient { get; private set; } = null!;

    /// <summary>
    /// playwright.config.ts's default `timeout: 60_000`, overridden per describe block via
    /// `test.describe.configure({ timeout })`. NUnit's [Timeout]/[CancelAfter] don't actually
    /// abort a hung Playwright call (see README), so this drives a real watchdog instead: it
    /// closes the browser context when it fires, which turns any in-flight Playwright call into
    /// a prompt PlaywrightException instead of a hang.
    /// </summary>
    protected virtual int TestTimeoutMilliseconds => 60_000;

    [OneTimeSetUp]
    public async Task PlaywrightTestBaseOneTimeSetUpAsync()
    {
        ServiceConfig = TestConfig.GetServiceConfigFromEnv();
        ApiConfig = ApiConfigFactory.GetApiConfigFromEnv();

        ApiClient = await ApiBase.CreateApiRequestContextAsync(AssemblySetup.Playwright, ApiConfig);
        AdminApiClient = await ApiBase.CreateApiRequestContextAsync(
            AssemblySetup.Playwright,
            ApiConfigFactory.ApiConfigForUser("admin", ApiConfig));
    }

    [OneTimeTearDown]
    public async Task PlaywrightTestBaseOneTimeTearDownAsync()
    {
        await ApiClient.DisposeAsync();
        await AdminApiClient.DisposeAsync();
    }

    public override BrowserNewContextOptions ContextOptions()
    {
        _artifactsDirectory = CurrentTestArtifactsDirectory();

        var options = new BrowserNewContextOptions(Playwright.Devices["Desktop Chrome"])
        {
            BaseURL = ServiceConfig.Url,
            IgnoreHTTPSErrors = true,
            RecordVideoDir = _artifactsDirectory,
        };

        var zapProxy = TestEnvironment.OptionalEnvironmentVariable("ZAP_PROXY");
        if (zapProxy is not null)
        {
            options.Proxy = new Proxy { Server = zapProxy };
        }

        return options;
    }

    [SetUp]
    public async Task PlaywrightTestBaseSetUpAsync()
    {
        // Skip SignalR when running under Cypress to avoid WebSocket proxy issues.
        await Page.AddInitScriptAsync("window.Cypress = true");
        await AuthenticationInterceptor.RegisterAuthenticationAsync(Context, ServiceConfig);

        Page.SetDefaultTimeout(10_000);
        Page.SetDefaultNavigationTimeout(15_000);

        await Context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true,
        });

        _timeoutWatchdog = new Timer(
            _ => _ = Context.CloseAsync(),
            null,
            TestTimeoutMilliseconds,
            Timeout.Infinite);
    }

    [TearDown]
    public async Task PlaywrightTestBaseTearDownAsync()
    {
        if (_timeoutWatchdog is not null)
        {
            await _timeoutWatchdog.DisposeAsync();
        }

        var passed = TestOk();

        try
        {
            await Context.Tracing.StopAsync(passed
                ? null
                : new TracingStopOptions { Path = Path.Combine(_artifactsDirectory, "trace.zip") });
        }
        catch
        {
            // Best-effort: a watchdog-forced close may already have invalidated the context.
        }

        if (!passed)
        {
            try
            {
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_artifactsDirectory, "screenshot.png") });
            }
            catch
            {
                // Best-effort: the page may already be gone if the watchdog fired.
            }
        }

        try
        {
            // Finalises the video file. Safe even if the base Playwright.NUnit teardown or our
            // own watchdog already closed the context - IBrowserContext.CloseAsync() no-ops when
            // called again.
            await Context.CloseAsync();
        }
        catch
        {
            // ignored
        }

        if (passed)
        {
            TryDeleteArtifacts();
        }
        else
        {
            AttachArtifacts();
        }
    }

    protected Task LoginAsync(string? userName = null) => Login.LoginAsync(Page, userName);

    private static string CurrentTestArtifactsDirectory()
    {
        var test = TestContext.CurrentContext.Test;
        var invalid = Path.GetInvalidFileNameChars();
        var safeName = new string($"{test.ClassName}.{test.Name}".Select(c => invalid.Contains(c) ? '_' : c).ToArray());

        var root = TestEnvironment.OptionalEnvironmentVariable("PW_ARTIFACTS_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(AppContext.BaseDirectory, "test-results");

        var directory = Path.Combine(root, safeName);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private void TryDeleteArtifacts()
    {
        try
        {
            Directory.Delete(_artifactsDirectory, recursive: true);
        }
        catch
        {
            // ignored - best-effort cleanup only.
        }
    }

    private void AttachArtifacts()
    {
        foreach (var file in Directory.EnumerateFiles(_artifactsDirectory))
        {
            TestContext.AddTestAttachment(file);
        }
    }
}
