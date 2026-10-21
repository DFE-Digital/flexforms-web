using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;
using NUnit.Framework;

// Deliberately no namespace: an NUnit [SetUpFixture] with no namespace applies to every test
// in the assembly, regardless of which namespace it lives in (Tests.*, in this case).

/// <summary>
/// Assembly-wide setup, equivalent to playwright.config.ts's load-env import and the base
/// expect timeout. Also owns a single shared IPlaywright instance used only for building API
/// request contexts in each fixture's [OneTimeSetUp] - separate from Microsoft.Playwright.NUnit's
/// own internal Playwright instance (which it only makes available per-test, not fixture-wide).
/// </summary>
[SetUpFixture]
public sealed class AssemblySetup
{
    public static IPlaywright Playwright { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        EnvFile.Load();
        Assertions.SetDefaultExpectTimeout(10_000);
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        WriteAccessibilityReport();
        Playwright.Dispose();
    }

    /// <summary>
    /// Writes the aggregated WCAG 2.2 AA findings gathered across every fixture's Page.Load
    /// hook (see PlaywrightTestBase) once the whole run has finished. Report-only: this never
    /// fails the build, regardless of what it finds.
    /// </summary>
    private static void WriteAccessibilityReport()
    {
        var results = AccessibilityReportCollector.Snapshot();
        if (results.Count == 0)
        {
            return;
        }

        var root = TestEnvironment.OptionalEnvironmentVariable("PW_ARTIFACTS_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(AppContext.BaseDirectory, "test-results");

        AccessibilityReportWriter.Write(results, root);
    }
}
