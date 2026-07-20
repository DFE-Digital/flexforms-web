using Azure.Core;
using Azure.Identity;
using GovUK.Dfe.FlexForms.Web.Configuration;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Services.Platform;

/// <inheritdoc />
public sealed class PlatformAccessTokenProvider(IOptions<PlatformBootstrapOptions> options) : IPlatformAccessTokenProvider
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TokenCredential _credential = CreateCredential(options.Value);
    private string? _cachedToken;
    private DateTimeOffset _expiresAtUtc;

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var bootstrap = options.Value;
        if (string.IsNullOrWhiteSpace(bootstrap.Scope))
        {
            throw new InvalidOperationException("PlatformBootstrap:Scope is required when platform bootstrap is enabled.");
        }

        if (HasValidCachedToken())
        {
            return _cachedToken!;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (HasValidCachedToken())
            {
                return _cachedToken!;
            }

            // Token is shared across requests; do not tie Entra acquisition to a single
            // request abort (e.g. user navigates away mid-load).
            var token = await _credential.GetTokenAsync(
                new TokenRequestContext([bootstrap.Scope]),
                CancellationToken.None);

            // Refresh a few minutes early to avoid edge-of-expiry failures.
            _cachedToken = token.Token;
            _expiresAtUtc = token.ExpiresOn.ToUniversalTime().AddMinutes(-5);
            return _cachedToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool HasValidCachedToken() =>
        !string.IsNullOrEmpty(_cachedToken) && DateTimeOffset.UtcNow < _expiresAtUtc;

    private static TokenCredential CreateCredential(PlatformBootstrapOptions bootstrap) =>
        !string.IsNullOrWhiteSpace(bootstrap.ClientSecret)
            ? new ClientSecretCredential(
                bootstrap.TenantId,
                bootstrap.ClientId,
                bootstrap.ClientSecret)
            : new DefaultAzureCredential();
}
