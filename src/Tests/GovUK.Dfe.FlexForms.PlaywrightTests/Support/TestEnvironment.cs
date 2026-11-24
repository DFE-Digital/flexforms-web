namespace GovUK.Dfe.FlexForms.PlaywrightTests.Support;

public static class TestEnvironment
{
    public static string RequireEnvironmentVariable(string name)
    {
        var value = OptionalEnvironmentVariable(name);

        if (value is null)
        {
            throw new InvalidOperationException($"{name} is required. Set it in .env locally or as a GitHub environment variable in CI.");
        }

        return value;
    }

    public static string? OptionalEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name)?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
