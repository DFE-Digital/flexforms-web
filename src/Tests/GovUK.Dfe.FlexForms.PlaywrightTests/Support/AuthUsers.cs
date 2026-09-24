namespace GovUK.Dfe.FlexForms.PlaywrightTests.Support;

public static class AuthUsers
{
    public static readonly IReadOnlyList<string> Names = ["default", "admin", "caseworker"];

    private static readonly IReadOnlyDictionary<string, (string EmailVariable, string ApiKeyVariable)> EnvironmentVariables =
        new Dictionary<string, (string, string)>
        {
            ["default"] = ("DEFAULT_USER_EMAIL", "DEFAULT_USER_API_KEY"),
            ["admin"] = ("ADMIN_EMAIL", "ADMIN_API_KEY"),
            ["caseworker"] = ("CASEWORKER_EMAIL", "CASEWORKER_API_KEY"),
        };

    public static AuthUser GetDefaultAuthUser() => LoadAuthUser("default");

    public static AuthUser ResolveAuthUser(string? userName = null)
    {
        var name = string.IsNullOrWhiteSpace(userName) ? "default" : userName.Trim().ToLowerInvariant();

        if (!Names.Contains(name))
        {
            throw new InvalidOperationException($"Unknown auth user '{userName}'. Expected one of: {string.Join(", ", Names)}");
        }

        return LoadAuthUser(name);
    }

    private static AuthUser LoadAuthUser(string name)
    {
        var variables = EnvironmentVariables[name];

        return new AuthUser(
            name,
            TestEnvironment.RequireEnvironmentVariable(variables.EmailVariable),
            TestEnvironment.RequireEnvironmentVariable(variables.ApiKeyVariable));
    }
}
