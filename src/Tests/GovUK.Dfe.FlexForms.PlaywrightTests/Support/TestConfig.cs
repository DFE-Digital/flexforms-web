namespace GovUK.Dfe.FlexForms.PlaywrightTests.Support;

public static class TestConfig
{
    public static ServiceConfig GetServiceConfigFromEnv()
    {
        var defaultUser = AuthUsers.GetDefaultAuthUser();

        return new ServiceConfig(
            Url: NormalizeUrl(TestEnvironment.RequireEnvironmentVariable("BASE_URL")),
            Username: defaultUser.Email,
            ApiKey: defaultUser.ApiKey,
            TenantId: TestEnvironment.RequireEnvironmentVariable("TENANT_ID"),
            Terminology: new Terminology(
                TestEnvironment.RequireEnvironmentVariable("TERMINOLOGY_SINGULAR"),
                TestEnvironment.RequireEnvironmentVariable("TERMINOLOGY_PLURAL")));
    }

    private static string NormalizeUrl(string url) => url.EndsWith('/') ? url[..^1] : url;
}
