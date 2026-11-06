using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;
using NUnit.Framework;

// Deliberately no namespace: an NUnit [SetUpFixture] with no namespace applies to every test
// in the assembly, regardless of which namespace it lives in (Tests.*, in this case).

// Opts every fixture into NUnit's parallel executor. ParallelScope.Fixtures parallelises across
// *fixtures* (classes) only: the tests inside one fixture still run one after another.
// A fixture marked [NonParallelizable] never runs alongside anything else. Use it for fixtures that
// change tenant-wide state (terminology, dashboard config, status labels, live templates, users and
// roles), not for fixtures whose state is private to them.
//
// Application creation is serialised in ApplicationCreationGate, because the API's reference
// generator ("latest today + 1") is not safe for concurrent creates: two at once collide on the
// unique IX_Applications_ApplicationReference index. That is the most likely cause of the
// "DB race condition" that got the first attempt at parallel runs reverted.
//
// CI overrides the worker count with NUnit.NumberOfTestWorkers in playwright.yml; keep the two equal.
[assembly: Parallelizable(ParallelScope.Fixtures)]
[assembly: LevelOfParallelism(4)]

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
        Playwright.Dispose();
    }
}
