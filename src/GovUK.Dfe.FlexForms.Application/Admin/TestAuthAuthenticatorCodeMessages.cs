namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// User-facing copy for creating Test Authentication authenticator app setup codes.
/// </summary>
public static class TestAuthAuthenticatorCodeMessages
{
    public const string EmailRequired = "Enter an email address";

    public const string EmailInvalid = "Enter an email address in the correct format, like name@example.com";

    public const string CreateFailed = "Could not create a setup code.";

    public static string Created(string email) => $"Setup code created for {email}.";
}
