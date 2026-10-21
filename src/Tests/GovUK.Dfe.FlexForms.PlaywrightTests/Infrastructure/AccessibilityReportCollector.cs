using System.Collections.Concurrent;
using Deque.AxeCore.Commons;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;

/// <summary>
/// One page visit's accessibility scan result, as recorded by <see cref="AccessibilityReportCollector"/>.
/// </summary>
public sealed record AccessibilityPageResult(string TestName, string Url, IReadOnlyList<AxeResultItem> Violations);

/// <summary>
/// Assembly-wide, thread-safe collector of accessibility scan results gathered automatically
/// as every <see cref="PlaywrightTestBase"/>-derived fixture visits pages (see its
/// <c>Page.Load</c> hook). Findings are reported, not enforced: this never fails a test: see
/// <see cref="AssemblySetup"/>'s one-time teardown, which drains this collector once the whole
/// run has finished and writes the final report via <see cref="AccessibilityReportWriter"/>.
/// </summary>
public static class AccessibilityReportCollector
{
    private static readonly ConcurrentBag<AccessibilityPageResult> Results = [];

    public static void Record(AccessibilityPageResult result) => Results.Add(result);

    public static IReadOnlyList<AccessibilityPageResult> Snapshot() => [.. Results];
}
