using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Sends a payload to every other Web replica over a Redis pub/sub channel and, as a hosted service,
/// applies the payloads they send. Used to drop per-replica in-memory caches after a change.
/// </summary>
/// <remarks>
/// Redis pub/sub does not queue: a replica that is disconnected when a message is sent never sees it,
/// so every cache that relies on this must still expire on its own.
/// </remarks>
public abstract class ReplicaBroadcastService(
    RedisChannel channel,
    IServiceProvider serviceProvider,
    ILogger logger) : BackgroundService
{
    private const char Separator = '|';

    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    protected ILogger Logger { get; } = logger;

    internal TimeSpan SubscribeRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Applies a payload published by another replica. Messages this replica sent are not passed here.
    /// </summary>
    protected abstract void OnRemoteMessage(string payload);

    /// <summary>
    /// Publishes a payload to the other replicas. Failures are logged, never thrown, so the caller's change still succeeds.
    /// </summary>
    protected async Task PublishAsync(string payload)
    {
        try
        {
            await GetRedis().GetSubscriber().PublishAsync(channel, $"{payload}{Separator}{_instanceId}");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Could not publish to {Channel}; other Web replicas keep their copy until it expires",
                channel.ToString());
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GetRedis().GetSubscriber().SubscribeAsync(channel, (_, message) => OnMessage(message));
                Logger.LogInformation("Listening on {Channel} for changes from other Web replicas", channel.ToString());
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                Logger.LogWarning(
                    ex,
                    "Could not subscribe to {Channel}; retrying in {RetryDelay}",
                    channel.ToString(),
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
        var text = message.ToString();
        var separatorIndex = text.LastIndexOf(Separator);
        if (separatorIndex <= 0 || separatorIndex == text.Length - 1)
        {
            Logger.LogWarning("Ignoring an unreadable message on {Channel}", channel.ToString());
            return;
        }

        if (text[(separatorIndex + 1)..] == _instanceId)
        {
            return;
        }

        try
        {
            OnRemoteMessage(text[..separatorIndex]);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not apply a message on {Channel}", channel.ToString());
        }
    }

    // Resolved per call: the CoreLibs registration connects on first resolve and throws while Redis is unreachable.
    private IConnectionMultiplexer GetRedis() => serviceProvider.GetRequiredService<IConnectionMultiplexer>();
}
