using System.Net.Http.Json;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api.Auth;

public static class Exchange
{
    private static readonly HttpClient HttpClient = new();

    public static string ResolveAccessToken(ExchangeTokenResponse response) =>
        response.AccessToken ?? throw new ApiRequestException("Token exchange response did not include an access token");

    public static async Task<string> ExchangeForInternalUserTokenAsync(ApiConfig config, string signInToken)
    {
        var body = new ExchangeTokenRequest(signInToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl}/v1/Tokens/exchange")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Api-Key", config.AuthProviderApiKey);
        request.Headers.Add("X-Tenant-ID", config.TenantId);
        request.Headers.Add("x-service-email", config.ServiceEmail);
        request.Headers.Add("x-service-api-key", config.ServiceApiKey);

        using var response = await HttpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new ApiRequestException($"Token exchange failed ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync()}");
        }

        var exchangeResponse = await response.Content.ReadFromJsonAsync<ExchangeTokenResponse>()
            ?? throw new ApiRequestException("Token exchange response body was empty.");

        return ResolveAccessToken(exchangeResponse);
    }
}
