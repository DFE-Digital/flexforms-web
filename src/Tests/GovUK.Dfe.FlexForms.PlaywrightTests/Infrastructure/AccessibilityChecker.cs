using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;

/// <summary>
/// Runs an axe-core accessibility scan against a page. axe-core has no single tag covering
/// "WCAG 2.2 AA" end to end, so need to include every tag explicitly.
/// </summary>
public static class AccessibilityChecker
{
    private static readonly List<string> Wcag22AaTags =
    [
        "wcag2a",
        "wcag2aa",
        "wcag21a",
        "wcag21aa",
        "wcag22aa",
    ];

    public static async Task<AxeResult> ScanAsync(IPage page)
    {
        var options = new AxeRunOptions
        {
            RunOnly = new RunOnlyOptions { Type = "tag", Values = Wcag22AaTags },
        };

        return await page.RunAxe(options);
    }
}
