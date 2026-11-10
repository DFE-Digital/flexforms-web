using GovUK.Dfe.CoreLibs.Caching.Settings;
using GovUK.Dfe.FlexForms.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Caching;

public class TwoLevelCacheTests
{
    private const string Key = "FormTemplate_abc123";
    private const string Method = "GetTemplateAsync";

    private readonly InMemorySharedCacheStore _redis = new();

    [Fact]
    public async Task SecondRead_IsServedFromMemory()
    {
        var replica = new Replica(_redis);
        var fetches = 0;

        await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult($"v{++fetches}"), Method);
        var second = await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult($"v{++fetches}"), Method);

        Assert.Equal("v1", second);
        Assert.Equal(1, fetches);
    }

    [Fact]
    public async Task AnotherReplica_ReadsTheSharedCopy_WithoutFetching()
    {
        await new Replica(_redis).Cache.GetOrAddAsync(Key, () => Task.FromResult("v1"), Method);

        var fromOther = await new Replica(_redis).Cache.GetOrAddAsync(
            Key, () => Task.FromResult("fetched again"), Method);

        Assert.Equal("v1", fromOther);
    }

    [Fact]
    public async Task RemoveAsync_InvalidatesRedis_DropsMemory_AndPublishes()
    {
        var replica = new Replica(_redis);
        await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("v1"), Method);

        await replica.Cache.RemoveAsync(Key);

        Assert.False(_redis.HasValue(Key));
        Assert.Equal("v2", await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("v2"), Method));
        Assert.StartsWith($"{Key}|", Assert.Single(replica.Published).ToString());
    }

    [Fact]
    public async Task OldFetchFinishingAfterRemoval_DoesNotPutTheOldValueBackInRedis()
    {
        var slow = new Replica(_redis);
        await slow.StartAsync();
        var fetchStarted = new TaskCompletionSource();
        var oldTemplate = new TaskCompletionSource<string>();
        var inFlight = slow.Cache.GetOrAddAsync(Key, () =>
        {
            fetchStarted.SetResult();
            return oldTemplate.Task;
        }, Method);
        await fetchStarted.Task;

        var saving = new Replica(_redis);
        await saving.Cache.RemoveAsync(Key);
        slow.Deliver(saving.Published.Single());
        oldTemplate.SetResult("old");
        await inFlight;

        Assert.False(_redis.HasValue(Key));
        var fresh = await new Replica(_redis).Cache.GetOrAddAsync(Key, () => Task.FromResult("new"), Method);
        Assert.Equal("new", fresh);
        Assert.Equal("new", await slow.Cache.GetOrAddAsync(Key, () => Task.FromResult("new"), Method));
    }

    [Fact]
    public async Task MessageFromAnotherReplica_DropsThisReplicasMemoryCopy()
    {
        var saving = new Replica(_redis);
        var other = new Replica(_redis);
        await other.StartAsync();
        await other.Cache.GetOrAddAsync(Key, () => Task.FromResult("v1"), Method);

        await saving.Cache.RemoveAsync(Key);
        other.Deliver(saving.Published.Single());

        Assert.Equal("v2", await other.Cache.GetOrAddAsync(Key, () => Task.FromResult("v2"), Method));
    }

    [Fact]
    public async Task OwnMessage_IsIgnored()
    {
        var replica = new Replica(_redis);
        await replica.StartAsync();
        await replica.Cache.RemoveAsync(Key);
        await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("v1"), Method);

        replica.Deliver(replica.Published.Single());

        Assert.Equal("v1", await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("v2"), Method));
    }

    [Fact]
    public async Task RedisUnavailable_DoesNotStopStartup_AndReadsFallBackToTheSource()
    {
        var replica = new Replica(_redis, redisAvailable: false);

        await replica.Cache.StartAsync(CancellationToken.None);
        var value = await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("from source"), Method);
        await replica.Cache.RemoveAsync(Key);
        await replica.Cache.StopAsync(CancellationToken.None);

        Assert.Equal("from source", value);
        Assert.Equal(0, _redis.Writes);
    }

    [Fact]
    public async Task ConcurrentMisses_OnOneReplica_FetchOnce()
    {
        var replica = new Replica(_redis);
        var fetches = 0;
        var release = new TaskCompletionSource<string>();

        var reads = Enumerable.Range(0, 5)
            .Select(_ => replica.Cache.GetOrAddAsync(Key, () =>
            {
                Interlocked.Increment(ref fetches);
                return release.Task;
            }, Method))
            .ToList();
        release.SetResult("v1");

        Assert.All(await Task.WhenAll(reads), value => Assert.Equal("v1", value));
        Assert.Equal(1, fetches);
    }

    private sealed class Replica
    {
        private readonly IServiceProvider _services = Substitute.For<IServiceProvider>();
        private Action<RedisChannel, RedisValue>? _handler;

        public Replica(ISharedCacheStore store, bool redisAvailable = true)
        {
            var subscriber = Substitute.For<ISubscriber>();
            subscriber
                .PublishAsync(TwoLevelCache.Channel, Arg.Do<RedisValue>(Published.Add), Arg.Any<CommandFlags>())
                .Returns(1L);
            subscriber
                .SubscribeAsync(
                    TwoLevelCache.Channel,
                    Arg.Do<Action<RedisChannel, RedisValue>>(handler =>
                    {
                        _handler = handler;
                        Subscribed.TrySetResult();
                    }),
                    Arg.Any<CommandFlags>())
                .Returns(Task.CompletedTask);

            var multiplexer = Substitute.For<IConnectionMultiplexer>();
            multiplexer.GetSubscriber(Arg.Any<object?>()).Returns(subscriber);
            var unavailable = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is down");
            _services.GetService(typeof(IConnectionMultiplexer))
                .Returns(_ => redisAvailable ? multiplexer : throw unavailable);
            _services.GetService(typeof(ISharedCacheStore))
                .Returns(_ => redisAvailable ? store : throw unavailable);

            Cache = new TwoLevelCache(
                new MemoryCache(new MemoryCacheOptions()),
                Options.Create(new CacheSettings
                {
                    Memory = new MemoryCacheSettings { DefaultDurationInSeconds = 60 },
                    Redis = new RedisCacheSettings { DefaultDurationInSeconds = 300 }
                }),
                _services,
                NullLogger<TwoLevelCache>.Instance)
            {
                SubscribeRetryDelay = TimeSpan.FromMilliseconds(10)
            };
        }

        public TwoLevelCache Cache { get; }

        public List<RedisValue> Published { get; } = [];

        public TaskCompletionSource Subscribed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task StartAsync()
        {
            await Cache.StartAsync(CancellationToken.None);
            await Subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void Deliver(RedisValue message) => _handler!(TwoLevelCache.Channel, message);
    }
}
