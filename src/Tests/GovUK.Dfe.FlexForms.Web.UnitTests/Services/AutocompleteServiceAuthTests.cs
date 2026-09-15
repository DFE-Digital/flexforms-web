using System.Net;
using System.Net.Http.Headers;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class AutocompleteServiceAuthTests
{
    [Fact]
    public async Task SearchAsync_ShouldSendApiKeyHeader_WhenApiKeyIsConfigured()
    {
        HttpRequestMessage? captured = null;
        var httpClient = CreateHttpClient(request =>
        {
            captured = request;
            return JsonArray("Acme Trust");
        });
        var service = CreateService(httpClient, new ComplexFieldConfiguration
        {
            Id = "trusts",
            ApiEndpoint = "https://example.test/search",
            ApiKey = "secret-key"
        });

        await service.SearchAsync("trusts", "acme");

        Assert.NotNull(captured);
        Assert.Equal("secret-key", captured!.Headers.GetValues("ApiKey").Single());
        Assert.Null(captured.Headers.Authorization);
    }

    [Fact]
    public async Task SearchAsync_ShouldSendBearerToken_WhenClientCredentialsAreConfigured()
    {
        HttpRequestMessage? captured = null;
        var httpClient = CreateHttpClient(request =>
        {
            captured = request;
            return JsonArray("Acme Trust");
        });
        var tokens = Substitute.For<IAutocompleteAccessTokenProvider>();
        tokens.GetAccessTokenAsync(Arg.Any<ComplexFieldConfiguration>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("access-token");
        var service = CreateService(httpClient, new ComplexFieldConfiguration
        {
            Id = "orgs",
            ApiEndpoint = "https://example.test/orgs",
            AuthType = "ClientCredentials",
            TokenEndpoint = "https://login.example.test/token",
            ClientId = "id",
            ClientSecret = "secret"
        }, tokens);

        await service.SearchAsync("orgs", "acme");

        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("access-token", captured.Headers.Authorization?.Parameter);
        Assert.False(captured.Headers.Contains("ApiKey"));
    }

    [Fact]
    public async Task SearchAsync_ShouldRetryWithRefreshedToken_WhenFirstResponseIsUnauthorized()
    {
        var attempt = 0;
        var authorizations = new List<string?>();
        var httpClient = CreateHttpClient(request =>
        {
            attempt++;
            authorizations.Add(request.Headers.Authorization?.Parameter);
            return attempt == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : JsonArray("Acme Trust");
        });
        var tokens = Substitute.For<IAutocompleteAccessTokenProvider>();
        tokens.GetAccessTokenAsync(Arg.Any<ComplexFieldConfiguration>(), false, Arg.Any<CancellationToken>())
            .Returns("stale-token");
        tokens.GetAccessTokenAsync(Arg.Any<ComplexFieldConfiguration>(), true, Arg.Any<CancellationToken>())
            .Returns("fresh-token");
        var service = CreateService(httpClient, new ComplexFieldConfiguration
        {
            Id = "orgs",
            ApiEndpoint = "https://example.test/orgs",
            TokenEndpoint = "https://login.example.test/token",
            ClientId = "id",
            ClientSecret = "secret"
        }, tokens);

        var results = await service.SearchAsync("orgs", "acme");

        Assert.Equal(2, attempt);
        Assert.Equal(["stale-token", "fresh-token"], authorizations);
        Assert.Single(results);
    }

    [Fact]
    public async Task SearchAsync_ShouldSendRequestWithoutAuth_WhenTokenProviderReturnsNoToken()
    {
        HttpRequestMessage? captured = null;
        var httpClient = CreateHttpClient(request =>
        {
            captured = request;
            return JsonArray("Acme Trust");
        });
        var tokens = Substitute.For<IAutocompleteAccessTokenProvider>();
        tokens.GetAccessTokenAsync(Arg.Any<ComplexFieldConfiguration>(), false, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        var service = CreateService(httpClient, new ComplexFieldConfiguration
        {
            Id = "orgs",
            ApiEndpoint = "https://example.test/orgs",
            AuthType = "ClientCredentials",
            TokenEndpoint = "https://login.example.test/token",
            ClientId = "id",
            ClientSecret = "secret"
        }, tokens);

        var results = await service.SearchAsync("orgs", "acme");

        Assert.Single(results);
        Assert.NotNull(captured);
        Assert.Null(captured!.Headers.Authorization);
        Assert.False(captured.Headers.Contains("ApiKey"));
    }

    [Fact]
    public async Task SearchAsync_ShouldCopyAllScalarAndNestedNameProperties()
    {
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                [{
                  "id": 123,
                  "displayName": "Jane Smith",
                  "active": true,
                  "retired": false,
                  "constituency": { "name": "Example West" },
                  "empty": "",
                  "ignored": ["value"]
                }]
                """)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
            }
        });
        var service = CreateService(httpClient, new ComplexFieldConfiguration
        {
            Id = "members",
            ApiEndpoint = "https://example.test/members",
            MinLength = 1
        });

        var results = await service.SearchAsync("members", "jane");

        var result = Assert.IsType<Dictionary<string, object>>(Assert.Single(results));
        Assert.Equal("123", result["id"]);
        Assert.Equal("Jane Smith", result["name"]);
        Assert.Equal("Jane Smith", result["displayName"]);
        Assert.Equal("True", result["active"]);
        Assert.Equal("False", result["retired"]);
        Assert.Equal("Example West", result["constituency"]);
        Assert.False(result.ContainsKey("empty"));
        Assert.False(result.ContainsKey("ignored"));
    }

    [Fact]
    public async Task SearchAsync_ShouldRetryOnlyOnce_WhenRefreshedTokenIsAlsoUnauthorized()
    {
        var attempts = 0;
        var httpClient = CreateHttpClient(_ =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("unauthorized")
            };
        });
        var tokens = Substitute.For<IAutocompleteAccessTokenProvider>();
        tokens.GetAccessTokenAsync(Arg.Any<ComplexFieldConfiguration>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => (bool)call[1] ? "fresh-token" : "stale-token");
        var service = CreateService(httpClient, new ComplexFieldConfiguration
        {
            Id = "orgs",
            ApiEndpoint = "https://example.test/orgs",
            AuthType = "ClientCredentials",
            TokenEndpoint = "https://login.example.test/token",
            ClientId = "id",
            ClientSecret = "secret"
        }, tokens);

        Assert.Empty(await service.SearchAsync("orgs", "acme"));
        Assert.Equal(2, attempts);
    }

    private static AutocompleteService CreateService(
        HttpClient httpClient,
        ComplexFieldConfiguration configuration,
        IAutocompleteAccessTokenProvider? tokens = null)
    {
        var configs = Substitute.For<IComplexFieldConfigurationService>();
        configs.GetConfiguration(configuration.Id).Returns(configuration);
        return new AutocompleteService(
            httpClient,
            configs,
            tokens ?? Substitute.For<IAutocompleteAccessTokenProvider>(),
            NullLogger<AutocompleteService>.Instance);
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new StubHandler(responder));

    private static HttpResponseMessage JsonArray(string name) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent($"[{{\"name\":\"{name}\"}}]")
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
            }
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
