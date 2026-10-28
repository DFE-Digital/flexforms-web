using System.ComponentModel;
using System.Diagnostics;
using Xunit.Abstractions;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.JavaScript;

/// <summary>
/// Runs the Template Studio JavaScript tests in this folder with Node's built-in test runner.
/// </summary>
public class TemplateStudioScriptTests(ITestOutputHelper output)
{
    [Fact]
    public async Task TemplateDocumentScript_ShouldPassItsTests()
    {
        var testFile = FindTestFile("template-document.test.js");

        string result;
        int exitCode;
        try
        {
            using var node = Process.Start(new ProcessStartInfo("node", ["--test", testFile])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(testFile)!
            })!;
            var stdout = node.StandardOutput.ReadToEndAsync();
            var stderr = node.StandardError.ReadToEndAsync();
            await node.WaitForExitAsync();
            result = await stdout + await stderr;
            exitCode = node.ExitCode;
        }
        catch (Win32Exception) when (Environment.GetEnvironmentVariable("CI") is null)
        {
            output.WriteLine("Node isn't installed, so the Template Studio script tests didn't run.");
            return;
        }

        output.WriteLine(result);
        Assert.True(exitCode == 0, result);
    }

    private static string FindTestFile(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "JavaScript", name);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Couldn't find JavaScript/{name} above {AppContext.BaseDirectory}");
    }
}
