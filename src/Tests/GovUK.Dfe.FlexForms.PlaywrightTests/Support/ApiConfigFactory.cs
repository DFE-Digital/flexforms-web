namespace GovUK.Dfe.FlexForms.PlaywrightTests.Support;

public static class ApiConfigFactory
{
    public static ApiConfig CreateApiConfig()
    {
        var defaultUser = AuthUsers.GetDefaultAuthUser();

        return new ApiConfig(
            BaseUrl: TrimTrailingSlash(TestEnvironment.RequireEnvironmentVariable("API_BASE_URL")),
            TenantId: TestEnvironment.RequireEnvironmentVariable("TENANT_ID"),
            ServiceEmail: defaultUser.Email,
            ServiceApiKey: defaultUser.ApiKey,
            TemplateId: TestEnvironment.RequireEnvironmentVariable("TEMPLATE_ID"),
            InternalServiceAuth: new InternalServiceAuthSettings(
                SecretKey: TestEnvironment.RequireEnvironmentVariable("JWT_SIGNING_KEY"),
                Issuer: TestEnvironment.RequireEnvironmentVariable("INTERNAL_AUTH_ISSUER"),
                Audience: TestEnvironment.RequireEnvironmentVariable("INTERNAL_AUTH_AUDIENCE"),
                TokenLifetimeMinutes: ResolveTokenLifetimeMinutes()),
            AuthProviderApiKey: TestEnvironment.RequireEnvironmentVariable("AUTH_PROVIDER_API_KEY"));
    }

    public static ApiConfig GetApiConfigFromEnv() => CreateApiConfig();

    public static ApiConfig ApiConfigForUser(string userName, ApiConfig? baseConfig = null)
    {
        var config = baseConfig ?? CreateApiConfig();
        var user = AuthUsers.ResolveAuthUser(userName);

        return config with { ServiceEmail = user.Email, ServiceApiKey = user.ApiKey };
    }

    private static int ResolveTokenLifetimeMinutes()
    {
        var value = TestEnvironment.OptionalEnvironmentVariable("INTERNAL_AUTH_TOKEN_LIFETIME_MINUTES");

        if (string.IsNullOrEmpty(value))
        {
            return 10;
        }

        if (!int.TryParse(value, out var tokenLifetimeMinutes) || tokenLifetimeMinutes <= 0)
        {
            throw new InvalidOperationException("INTERNAL_AUTH_TOKEN_LIFETIME_MINUTES must be a positive integer.");
        }

        return tokenLifetimeMinutes;
    }

    private static string TrimTrailingSlash(string value) => value.EndsWith('/') ? value[..^1] : value;
}
