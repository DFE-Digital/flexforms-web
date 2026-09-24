using System.Collections.Concurrent;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api.Auth;

/// <summary>
/// The TS suite caches this token in a module-level Map, one per worker process. NUnit runs
/// fixtures concurrently in a single process, so the cache and its refresh are made thread-safe
/// with a per-key lock rather than relying on single-threaded access.
/// </summary>
public static class InternalUserToken
{
    private const long ExpiryBufferMs = 2 * 60 * 1000;

    private sealed record CachedToken(string Value, long ExpiresAtMs);

    private static readonly ConcurrentDictionary<string, CachedToken> CachedInternalUserTokens = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public static async Task<string> GetInternalUserTokenAsync(ApiConfig config)
    {
        var key = CacheKey(config);

        if (CachedInternalUserTokens.TryGetValue(key, out var cached) && IsCacheValid(cached))
        {
            return cached.Value;
        }

        var gate = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (CachedInternalUserTokens.TryGetValue(key, out cached) && IsCacheValid(cached))
            {
                return cached.Value;
            }

            var signInToken = Token.GenerateInternalServiceToken(config);
            var internalUserToken = await Exchange.ExchangeForInternalUserTokenAsync(config, signInToken);

            CachedInternalUserTokens[key] = new CachedToken(internalUserToken, ResolveTokenExpiryMs(internalUserToken));

            return internalUserToken;
        }
        finally
        {
            gate.Release();
        }
    }

    private static string CacheKey(ApiConfig config) => $"{config.TenantId}:{config.ServiceEmail.ToLowerInvariant()}";

    private static bool IsCacheValid(CachedToken cache) =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ExpiryBufferMs < cache.ExpiresAtMs;

    private static long ResolveTokenExpiryMs(string token)
    {
        var handler = new JsonWebTokenHandler();
        var jsonWebToken = handler.ReadJsonWebToken(token);

        if (!jsonWebToken.TryGetPayloadValue<long>("exp", out var expirySeconds))
        {
            throw new InvalidOperationException("Unable to decode internal user token expiry");
        }

        return expirySeconds * 1000;
    }
}
