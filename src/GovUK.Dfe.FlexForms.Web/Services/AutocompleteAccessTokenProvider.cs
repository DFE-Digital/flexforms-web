using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Obtains and caches OAuth2 client-credential tokens for autocomplete APIs.
/// </summary>
public sealed class AutocompleteAccessTokenProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<AutocompleteAccessTokenProvider> logger) : IAutocompleteAccessTokenProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public async Task<string?> GetAccessTokenAsync(
        ComplexFieldConfiguration configuration,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.UsesClientCredentials)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(configuration.TokenEndpoint)
            || string.IsNullOrWhiteSpace(configuration.ClientId)
            || string.IsNullOrWhiteSpace(configuration.ClientSecret))
        {
            logger.LogWarning(
                "Client credentials are incomplete for complex field {ComplexFieldId}. TokenEndpoint, ClientId and ClientSecret are required.",
                configuration.Id);
            return null;
        }

        var cacheKey = BuildCacheKey(configuration);
        if (!forceRefresh && TryGetCachedToken(cacheKey, out var cached))
        {
            return cached;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && TryGetCachedToken(cacheKey, out cached))
            {
                return cached;
            }

            var token = await RequestTokenAsync(configuration, cancellationToken);
            if (token is null)
            {
                _cache.TryRemove(cacheKey, out _);
                return null;
            }

            _cache[cacheKey] = token;
            return token.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool TryGetCachedToken(string cacheKey, out string? accessToken)
    {
        accessToken = null;
        if (_cache.TryGetValue(cacheKey, out var entry) && DateTimeOffset.UtcNow < entry.ExpiresAtUtc)
        {
            accessToken = entry.AccessToken;
            return true;
        }

        return false;
    }

    private async Task<CacheEntry?> RequestTokenAsync(
        ComplexFieldConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(AutocompleteAccessTokenProvider));
            using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = configuration.ClientId,
                ["client_secret"] = configuration.ClientSecret
            };

            if (!string.IsNullOrWhiteSpace(configuration.Scope))
            {
                form["scope"] = configuration.Scope;
            }

            request.Content = new FormUrlEncodedContent(form);

            var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Client credentials token request failed with status {StatusCode} for complex field {ComplexFieldId}. Response: {ErrorContent}",
                    response.StatusCode,
                    configuration.Id,
                    Truncate(body));
                return null;
            }

            var payload = JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions);
            if (string.IsNullOrWhiteSpace(payload?.AccessToken))
            {
                logger.LogWarning(
                    "Client credentials token response did not contain an access_token for complex field {ComplexFieldId}.",
                    configuration.Id);
                return null;
            }

            var lifetime = payload.ExpiresIn is > 0 ? TimeSpan.FromSeconds(payload.ExpiresIn.Value) : TimeSpan.FromMinutes(5);
            var expiresAt = DateTimeOffset.UtcNow.Add(lifetime).AddMinutes(-1);
            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                expiresAt = DateTimeOffset.UtcNow.AddSeconds(30);
            }

            return new CacheEntry(payload.AccessToken, expiresAt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to obtain client credentials token for complex field {ComplexFieldId}", configuration.Id);
            return null;
        }
    }

    private static string BuildCacheKey(ComplexFieldConfiguration configuration) =>
        string.Join('|', configuration.TokenEndpoint, configuration.ClientId, configuration.Scope ?? string.Empty);

    private static string Truncate(string? value) =>
        string.IsNullOrEmpty(value) || value.Length <= 500 ? value ?? string.Empty : value[..500];

    private sealed record CacheEntry(string AccessToken, DateTimeOffset ExpiresAtUtc);

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }
    }
}
