using System.Text.Json;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class InternalServiceAuthenticationServiceTests
{
    private const string SecretKey = "internal-service-secret-key-long-enough-for-hmacsha256";

    private readonly IInternalServiceAuthOptionsResolver _optionsResolver =
        Substitute.For<IInternalServiceAuthOptionsResolver>();

    [Theory]
    [InlineData("", "api-key")]
    [InlineData("   ", "api-key")]
    [InlineData("service@dfe.gov.uk", "")]
    [InlineData("service@dfe.gov.uk", "   ")]
    public void ValidateServiceCredentials_ShouldReturnFalse_WhenEitherCredentialIsBlank(
        string email,
        string apiKey)
    {
        _optionsResolver.Resolve().Returns(CreateOptions());
        _optionsResolver.ClearReceivedCalls();

        Assert.False(CreateService().ValidateServiceCredentials(email, apiKey));
        _optionsResolver.DidNotReceive().Resolve();
    }

    [Fact]
    public void ValidateServiceCredentials_ShouldReturnFalse_WhenServiceIsNotConfigured()
    {
        _optionsResolver.Resolve().Returns(CreateOptions());

        Assert.False(CreateService().ValidateServiceCredentials("unknown@dfe.gov.uk", "service-api-key"));
    }

    [Fact]
    public void ValidateServiceCredentials_ShouldReturnFalse_WhenApiKeyDiffersInValue()
    {
        _optionsResolver.Resolve().Returns(CreateOptions());

        Assert.False(CreateService().ValidateServiceCredentials("service@dfe.gov.uk", "service-api-kex"));
    }

    [Fact]
    public void ValidateServiceCredentials_ShouldReturnFalse_WhenApiKeyDiffersInLength()
    {
        _optionsResolver.Resolve().Returns(CreateOptions());

        Assert.False(CreateService().ValidateServiceCredentials("service@dfe.gov.uk", "service-api-key-longer"));
    }

    [Fact]
    public void ValidateServiceCredentials_ShouldReturnTrue_WhenCredentialsMatch()
    {
        _optionsResolver.Resolve().Returns(CreateOptions());

        Assert.True(CreateService().ValidateServiceCredentials("service@dfe.gov.uk", "service-api-key"));
    }

    [Fact]
    public void ValidateServiceCredentials_ShouldMatchTheServiceEmailCaseInsensitively()
    {
        _optionsResolver.Resolve().Returns(CreateOptions());

        Assert.True(CreateService().ValidateServiceCredentials("Service@DfE.gov.UK", "service-api-key"));
    }

    [Fact]
    public async Task GenerateServiceTokenAsync_ShouldThrow_WhenTenantHasNoSecretKey()
    {
        _optionsResolver.Resolve().Returns(CreateOptions(secretKey: null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().GenerateServiceTokenAsync("service@dfe.gov.uk"));

        Assert.Contains("InternalServiceAuth:SecretKey", exception.Message);
    }

    [Fact]
    public async Task GenerateServiceTokenAsync_ShouldMintATokenForTheResolvedTenant()
    {
        _optionsResolver.Resolve().Returns(CreateOptions());

        var token = await CreateService().GenerateServiceTokenAsync("service@dfe.gov.uk");

        var payload = ReadPayload(token);
        Assert.Equal("tenant-issuer", payload.GetProperty("iss").GetString());
        Assert.Equal("tenant-audience", payload.GetProperty("aud").GetString());
        Assert.Equal("service@dfe.gov.uk", payload.GetProperty("email").GetString());
        Assert.Equal("internal", payload.GetProperty("service_type").GetString());
    }

    [Fact]
    public async Task GenerateServiceTokenAsync_ShouldStillMintAToken_WhenNoLifetimeIsConfigured()
    {
        _optionsResolver.Resolve().Returns(CreateOptions(tokenLifetimeMinutes: 0));

        var token = await CreateService().GenerateServiceTokenAsync("service@dfe.gov.uk");

        Assert.Equal("service@dfe.gov.uk", ReadPayload(token).GetProperty("sub").GetString());
    }

    private InternalServiceAuthenticationService CreateService()
        => new(
            _optionsResolver,
            NullLoggerFactory.Instance,
            NullLogger<InternalServiceAuthenticationService>.Instance);

    private static InternalServiceAuthOptions CreateOptions(
        string? secretKey = SecretKey,
        int tokenLifetimeMinutes = 5)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalServiceAuth:SecretKey"] = secretKey,
                ["InternalServiceAuth:Issuer"] = "tenant-issuer",
                ["InternalServiceAuth:Audience"] = "tenant-audience",
                ["InternalServiceAuth:TokenLifetimeMinutes"] = tokenLifetimeMinutes.ToString(),
                ["InternalServiceAuth:Services:0:Email"] = "service@dfe.gov.uk",
                ["InternalServiceAuth:Services:0:ApiKey"] = "service-api-key"
            })
            .Build()
            .GetSection("InternalServiceAuth")
            .Get<InternalServiceAuthOptions>()!;

    private static JsonElement ReadPayload(string token)
    {
        var segments = token.Split('.');
        Assert.Equal(3, segments.Length);
        return JsonDocument.Parse(WebEncoders.Base64UrlDecode(segments[1])).RootElement;
    }
}
