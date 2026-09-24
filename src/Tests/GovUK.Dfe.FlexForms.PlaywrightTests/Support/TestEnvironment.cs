namespace GovUK.Dfe.FlexForms.PlaywrightTests.Support;

public static class TestEnvironment
{
    public static readonly IReadOnlyList<ServiceName> Applications =
        [ServiceName.Transfers, ServiceName.Lsrp, ServiceName.Visits, ServiceName.TestService];

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

    public static ServiceName RequireService()
    {
        var service = RequireEnvironmentVariable("SERVICE");

        if (!Enum.TryParse<ServiceName>(service, ignoreCase: false, out var serviceName) || !Applications.Contains(serviceName))
        {
            throw new InvalidOperationException($"SERVICE must be one of: {string.Join(", ", Applications)}");
        }

        return serviceName;
    }
}
