namespace GovUK.Dfe.FlexForms.PlaywrightTests.Support;

/// <summary>
/// Minimal .env loader (stands in for the TS suite's dotenv dependency). Existing process
/// environment variables always win; values already set are never overwritten.
/// </summary>
public static class EnvFile
{
    private static readonly object Gate = new();
    private static bool _loaded;

    public static void Load()
    {
        lock (Gate)
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            var path = ResolveEnvFilePath();
            if (path is null || !File.Exists(path))
            {
                return;
            }

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                if (line.StartsWith("export ", StringComparison.Ordinal))
                {
                    line = line["export ".Length..].TrimStart();
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = Unquote(line[(separatorIndex + 1)..].Trim());

                if (Environment.GetEnvironmentVariable(key) is null)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }

    private static string? ResolveEnvFilePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GovUK.Dfe.FlexForms.PlaywrightTests.csproj")))
            {
                return Path.Combine(directory.FullName, ".env");
            }

            directory = directory.Parent;
        }

        return null;
    }
}
