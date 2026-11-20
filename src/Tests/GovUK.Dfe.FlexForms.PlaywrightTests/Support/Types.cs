namespace GovUK.Dfe.FlexForms.PlaywrightTests.Support;

public sealed record Terminology(string Singular, string Plural);

public sealed record AuthUser(string Name, string Email, string ApiKey);

public sealed record ServiceConfig(
    string Url,
    string Username,
    string ApiKey,
    string TenantId,
    Terminology Terminology);

public sealed record InternalServiceAuthSettings(
    string SecretKey,
    string Issuer,
    string Audience,
    int TokenLifetimeMinutes);

public sealed record ApiConfig(
    string BaseUrl,
    string TenantId,
    string ServiceEmail,
    string ServiceApiKey,
    string TemplateId,
    InternalServiceAuthSettings InternalServiceAuth,
    string AuthProviderApiKey);
