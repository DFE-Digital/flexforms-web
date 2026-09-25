namespace GovUK.Dfe.FlexForms.PlaywrightTests.Support;

public static class TestConfig
{
    public static ServiceConfig CreateServiceConfig(ServiceName serviceName)
    {
        var defaultUser = AuthUsers.GetDefaultAuthUser();

        return new ServiceConfig(
            Name: serviceName,
            Url: NormalizeUrl(TestEnvironment.RequireEnvironmentVariable("BASE_URL")),
            Username: defaultUser.Email,
            ApiKey: defaultUser.ApiKey,
            TenantId: TestEnvironment.RequireEnvironmentVariable("TENANT_ID"),
            Terminology: new Terminology(
                TestEnvironment.RequireEnvironmentVariable("TERMINOLOGY_SINGULAR"),
                TestEnvironment.RequireEnvironmentVariable("TERMINOLOGY_PLURAL")));
    }

    public static ServiceConfig GetServiceConfigFromEnv() => CreateServiceConfig(TestEnvironment.RequireService());

    private static string NormalizeUrl(string url) => url.EndsWith('/') ? url[..^1] : url;
}
