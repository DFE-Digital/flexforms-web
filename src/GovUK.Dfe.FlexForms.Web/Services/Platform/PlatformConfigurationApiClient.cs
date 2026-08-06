using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Web.Configuration;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Services.Platform;

/// <summary>
/// HTTP client for platform configuration endpoints on the external applications API.
/// </summary>
public sealed class PlatformConfigurationApiClient(
    HttpClient httpClient,
    IPlatformAccessTokenProvider tokenProvider,
    IOptions<PlatformBootstrapOptions> options,
    ILogger<PlatformConfigurationApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PlatformHostConfigurationResponse> GetHostConfigurationAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl($"v1/host-config?target={Uri.EscapeDataString(target)}");
        return await GetAsync<PlatformHostConfigurationResponse>(url, cancellationToken);
    }

    public async Task<PlatformTenantResolutionResponse> ResolveTenantByHostnameAsync(
        string hostname,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl($"v1/tenant-config/resolve?hostname={Uri.EscapeDataString(hostname)}");
        return await GetAsync<PlatformTenantResolutionResponse>(url, cancellationToken);
    }

    public async Task<PlatformTenantConfigurationResponse> GetTenantConfigurationAsync(
        Guid tenantId,
        string target,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(
            $"v1/tenant-config/tenants/{tenantId:D}?target={Uri.EscapeDataString(target)}");
        return await GetAsync<PlatformTenantConfigurationResponse>(url, cancellationToken);
    }

    private string BuildUrl(string relativePath)
    {
        var baseUrl = options.Value.ApiBaseUrl?.TrimEnd('/')
            ?? throw new InvalidOperationException("PlatformBootstrap:ApiBaseUrl is required.");

        return $"{baseUrl}/{relativePath}";
    }

    private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var correlationId = Guid.NewGuid().ToString();
        request.Headers.TryAddWithoutValidation("x-correlationId", correlationId);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "Platform API call failed. Url={Url}, Status={Status}, Body={Body}",
                url,
                (int)response.StatusCode,
                body);
            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException($"Empty response from {url}");
    }
}
