using System.Text.Json;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Auth;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api;

public static class ApiBase
{
    private static readonly JsonSerializerOptions ResponseSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task<IAPIRequestContext> CreateApiRequestContextAsync(IPlaywright playwright, ApiConfig config)
    {
        var token = await InternalUserToken.GetInternalUserTokenAsync(config);

        return await playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = config.BaseUrl,
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["Authorization"] = $"Bearer {token}",
                ["X-Tenant-ID"] = config.TenantId,
            },
            IgnoreHTTPSErrors = true,
        });
    }

    public static async Task<TResponse> ApiRequestAsync<TResponse>(
        IAPIRequestContext request,
        string path,
        string method = "GET",
        object? data = null,
        Dictionary<string, string>? headers = null)
    {
        var response = await request.FetchAsync(path, new APIRequestContextOptions
        {
            Method = method,
            DataObject = data,
            Headers = headers,
        });

        if (!response.Ok)
        {
            throw new ApiRequestException($"API request failed ({response.Status}): {await response.TextAsync()}");
        }

        return (await response.JsonAsync<TResponse>(ResponseSerializerOptions))!;
    }
}
