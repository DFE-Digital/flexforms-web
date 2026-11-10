using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Infrastructure.Caching;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Web.Services.Tenant;

/// <summary>
/// Drops cached tenant configuration on every Web replica, not only the one that handled the settings change.
/// </summary>
public interface ITenantConfigurationInvalidator
{
    /// <summary>
    /// Clears this replica's cached configuration and hostname mappings, then tells the other replicas to do
    /// the same. A replica that misses the message reloads when its cache entry expires.
    /// </summary>
    Task InvalidateAsync(Guid tenantId);
}

/// <summary>
/// Publishes tenant configuration invalidations and applies the ones other replicas publish.
/// </summary>
public sealed class TenantConfigurationInvalidator(
    ITenantConfigurationCache cache,
    TenantHostnameCache hostnameCache,
    IServiceProvider serviceProvider,
    ILogger<TenantConfigurationInvalidator> logger)
    : ReplicaBroadcastService(Channel, serviceProvider, logger), ITenantConfigurationInvalidator
{
    internal static readonly RedisChannel Channel =
        RedisChannel.Literal(FlexFormsCacheKeys.TenantConfigurationInvalidatedChannel);

    /// <inheritdoc />
    public async Task InvalidateAsync(Guid tenantId)
    {
        InvalidateLocal(tenantId);
        await PublishAsync(tenantId.ToString("D"));
    }

    protected override void OnRemoteMessage(string payload)
    {
        if (!Guid.TryParse(payload, out var tenantId))
        {
            Logger.LogWarning("Ignoring an unreadable tenant configuration invalidation message");
            return;
        }

        InvalidateLocal(tenantId);
        Logger.LogInformation(
            "Dropped cached configuration for tenant {TenantId} after a change on another Web replica",
            tenantId);
    }

    private void InvalidateLocal(Guid tenantId)
    {
        cache.Invalidate(tenantId);
        hostnameCache.Clear();
    }
}
