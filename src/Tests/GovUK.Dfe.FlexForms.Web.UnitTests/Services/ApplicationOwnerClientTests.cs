using System.Net;
using System.Text;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Services;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class ApplicationOwnerClientTests
{
    [Fact]
    public async Task ChangeOwnerAsync_ShouldPutEmailAndDeserializeSuccess()
    {
        var applicationId = Guid.NewGuid();
        HttpRequestMessage? captured = null;
        string? requestBody = null;
        var client = CreateClient(
            (request, _) =>
            {
                captured = request;
                requestBody = request.Content is null
                    ? string.Empty
                    : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""{"applicationId":"{{applicationId}}","templateReadGranted":true}""",
                        Encoding.UTF8,
                        "application/json")
                };
            },
            "https://api.example.test/");

        var result = await client.ChangeOwnerAsync(applicationId, "new.owner@example.com");

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Put, captured!.Method);
        Assert.Equal(
            $"https://api.example.test/v1/Applications/{applicationId}/owner",
            captured.RequestUri!.ToString());
        Assert.Contains("application/json", captured.Headers.Accept.ToString());
        Assert.Contains("new.owner@example.com", requestBody, StringComparison.Ordinal);
        Assert.True(result.TemplateReadGranted);
    }

    [Fact]
    public async Task ChangeOwnerAsync_ShouldReturnMinimalResult_WhenResponseBodyIsEmpty()
    {
        var applicationId = Guid.NewGuid();
        var client = CreateClient(
            (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            },
            baseUrl: "https://api.example.test");

        var result = await client.ChangeOwnerAsync(applicationId, "owner@example.com");

        Assert.Equal(Guid.Empty, result.ApplicationId);
        Assert.False(result.TemplateReadGranted);
    }

    [Fact]
    public async Task ChangeOwnerAsync_ShouldThrowExternalApplicationsException_WhenApiFails()
    {
        var client = CreateClient(
            (_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("validation failed", Encoding.UTF8, "text/plain")
            },
            "https://api.example.test");

        var ex = await Assert.ThrowsAsync<ExternalApplicationsException>(() =>
            client.ChangeOwnerAsync(Guid.NewGuid(), "owner@example.com"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("400", ex.Message, StringComparison.Ordinal);
    }

    private static ApplicationOwnerClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder,
        string baseUrl) =>
        new(new HttpClient(new StubHandler(responder)), baseUrl);

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request, cancellationToken));
    }
}
