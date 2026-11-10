using GovUK.Dfe.FlexForms.Domain.Caching;
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
/// Publishes invalidations on a Redis channel and, as a hosted service, applies the ones other replicas publish.
/// </summary>
public sealed class TenantConfigurationInvalidator(
    ITenantConfigurationCache cache,
    TenantHostnameCache hostnameCache,
    IServiceProvider serviceProvider,
    ILogger<TenantConfigurationInvalidator> logger) : BackgroundService, ITenantConfigurationInvalidator
{
    internal static readonly RedisChannel Channel =
        RedisChannel.Literal(FlexFormsCacheKeys.TenantConfigurationInvalidatedChannel);

    private const char Separator = '|';

    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    internal TimeSpan SubscribeRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public async Task InvalidateAsync(Guid tenantId)
    {
        InvalidateLocal(tenantId);

        try
        {
            await GetRedis().GetSubscriber().PublishAsync(Channel, $"{tenantId:D}{Separator}{_instanceId}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not tell other Web replicas to drop cached configuration for tenant {TenantId}; they reload when their cache expires",
                tenantId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GetRedis().GetSubscriber().SubscribeAsync(Channel, (_, message) => OnMessage(message));
                logger.LogInformation("Listening for tenant configuration invalidations from other Web replicas");
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex,
                    "Could not subscribe to tenant configuration invalidations; retrying in {RetryDelay}",
                    SubscribeRetryDelay);
            }

            try
            {
                await Task.Delay(SubscribeRetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    internal void OnMessage(RedisValue message)
    {
        var parts = message.ToString().Split(Separator);
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var tenantId))
        {
            logger.LogWarning("Ignoring an unreadable tenant configuration invalidation message");
            return;
        }

        if (parts[1] == _instanceId)
        {
            return;
        }

        InvalidateLocal(tenantId);
        logger.LogInformation(
            "Dropped cached configuration for tenant {TenantId} after a change on another Web replica",
            tenantId);
    }

    private void InvalidateLocal(Guid tenantId)
    {
        cache.Invalidate(tenantId);
        hostnameCache.Clear();
    }

    // Resolved per call: the CoreLibs registration connects on first resolve and throws while Redis is unreachable.
    private IConnectionMultiplexer GetRedis() => serviceProvider.GetRequiredService<IConnectionMultiplexer>();
}
