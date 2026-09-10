using System.Net;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class AutocompleteAccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_ShouldReturnNull_WhenClientCredentialsAreNotConfigured()
    {
        var provider = CreateProvider((_, _) => throw new InvalidOperationException("Token endpoint should not be called"));

        var token = await provider.GetAccessTokenAsync(new ComplexFieldConfiguration
        {
            Id = "trusts",
            ApiKey = "key"
        });

        Assert.Null(token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldRequestAndCacheToken()
    {
        var calls = 0;
        string? body = null;
        Uri? uri = null;
        HttpMethod? method = null;
        var provider = CreateProvider(async (request, _) =>
        {
            calls++;
            method = request.Method;
            uri = request.RequestUri;
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return TokenResponse();
        });
        var config = ClientCredentialsConfig();

        var first = await provider.GetAccessTokenAsync(config);
        var second = await provider.GetAccessTokenAsync(config);

        Assert.Equal("access-token", first);
        Assert.Equal("access-token", second);
        Assert.Equal(1, calls);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("https://login.example.test/token", uri!.ToString());
        Assert.Contains("grant_type=client_credentials", body);
        Assert.Contains("client_id=client-id", body);
        Assert.Contains("scope=api%3A%2F%2Fexample%2F.default", body);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldBypassCache_WhenForceRefreshIsTrue()
    {
        var calls = 0;
        var provider = CreateProvider((_, _) =>
        {
            calls++;
            return Task.FromResult(TokenResponse($"token-{calls}"));
        });
        var config = ClientCredentialsConfig();

        var first = await provider.GetAccessTokenAsync(config);
        var refreshed = await provider.GetAccessTokenAsync(config, forceRefresh: true);

        Assert.Equal("token-1", first);
        Assert.Equal("token-2", refreshed);
        Assert.Equal(2, calls);
    }

    private static AutocompleteAccessTokenProvider CreateProvider(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new StubHandler(responder)));
        return new AutocompleteAccessTokenProvider(factory, NullLogger<AutocompleteAccessTokenProvider>.Instance);
    }

    private static ComplexFieldConfiguration ClientCredentialsConfig() =>
        new()
        {
            Id = "orgs",
            AuthType = "ClientCredentials",
            TokenEndpoint = "https://login.example.test/token",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            Scope = "api://example/.default"
        };

    private static HttpResponseMessage TokenResponse(string token = "access-token") =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"access_token\":\"{token}\",\"expires_in\":3600}}")
        };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request, cancellationToken);
    }
}
