using System.Text;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;

/// <summary>
/// Writes the accumulated <see cref="AccessibilityReportCollector"/> findings to disk once,
/// at the end of the whole test run (see <see cref="AssemblySetup"/>'s one-time teardown).
/// </summary>
public static class AccessibilityReportWriter
{
    public static void Write(IReadOnlyList<AccessibilityPageResult> results, string directory)
    {
        Directory.CreateDirectory(directory);

        WriteJson(results, Path.Combine(directory, "accessibility-report.json"));
        WriteMarkdown(results, Path.Combine(directory, "accessibility-report.md"));
    }

    private static void WriteJson(IReadOnlyList<AccessibilityPageResult> results, string path)
    {
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteMarkdown(IReadOnlyList<AccessibilityPageResult> results, string path)
    {
        var withViolations = results.Where(r => r.Violations.Count > 0).OrderBy(r => r.Url).ToList();
        var cleanUrls = results
            .Where(r => r.Violations.Count == 0)
            .Select(r => r.Url)
            .Distinct()
            .OrderBy(url => url)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("# Accessibility report (WCAG 2.2 AA)");
        builder.AppendLine();
        builder.AppendLine(
            $"Scanned {results.Count} page visit(s) during this test run. "
            + "This report is informational only: violations do not fail the build.");
        builder.AppendLine();

        if (withViolations.Count == 0)
        {
            builder.AppendLine("No violations found. :white_check_mark:");
            File.WriteAllText(path, builder.ToString());
            return;
        }

        builder.AppendLine($"## Pages with violations ({withViolations.Count})");
        builder.AppendLine();
        builder.AppendLine("| Page | Test | Violations |");
        builder.AppendLine("| --- | --- | --- |");

        foreach (var result in withViolations)
        {
            builder.AppendLine($"| {result.Url} | {result.TestName} | {result.Violations.Count} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Violation detail");

        foreach (var result in withViolations)
        {
            builder.AppendLine();
            builder.AppendLine($"### {result.Url}");
            builder.AppendLine($"_Test: {result.TestName}_");
            builder.AppendLine();

            foreach (var violation in result.Violations)
            {
                builder.AppendLine($"- **{violation.Id}** ({violation.Impact}): {violation.Help}");
                builder.AppendLine($"  - Help: {violation.HelpUrl}");
                builder.AppendLine($"  - Affected elements: {violation.Nodes.Length}");

                foreach (var node in violation.Nodes.Take(5))
                {
                    var selector = node.Target.Selector ?? node.Target.ToString();
                    builder.AppendLine($"    - `{selector}`");
                }

                if (violation.Nodes.Length > 5)
                {
                    builder.AppendLine($"    - ...and {violation.Nodes.Length - 5} more");
                }
            }
        }

        if (cleanUrls.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"## Scanned clean ({cleanUrls.Count})");
            builder.AppendLine();

            foreach (var url in cleanUrls)
            {
                builder.AppendLine($"- {url}");
            }
        }

        File.WriteAllText(path, builder.ToString());
    }
}
