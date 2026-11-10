using GovUK.Dfe.CoreLibs.Caching.Interfaces;
using GovUK.Dfe.CoreLibs.Caching.Settings;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class TwoLevelCacheTests
{
    private const string Key = "FormTemplate_abc123";
    private const string Method = "GetTemplateAsync";

    [Fact]
    public async Task SecondRead_IsServedFromMemory_WithoutCallingRedis()
    {
        var replica = new Replica();
        replica.RedisReturns("v1");

        await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("source"), Method);
        var second = await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("source"), Method);

        Assert.Equal("v1", second);
        await replica.Redis.Received(1).GetOrAddAsync(Key, Arg.Any<Func<Task<string>>>(), Method, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_DeletesRedis_DropsMemory_AndPublishes()
    {
        var replica = new Replica();
        replica.RedisReturns("v1");
        await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("source"), Method);

        await replica.Cache.RemoveAsync(Key);
        replica.RedisReturns("v2");
        var afterRemove = await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("source"), Method);

        await replica.Redis.Received(1).RemoveAsync(Key);
        Assert.Equal("v2", afterRemove);
        Assert.StartsWith($"{Key}|", Assert.Single(replica.Published).ToString());
    }

    [Fact]
    public async Task MessageFromAnotherReplica_DropsThisReplicasMemoryCopy()
    {
        var saving = new Replica();
        var other = new Replica();
        await other.StartAsync();
        other.RedisReturns("v1");
        await other.Cache.GetOrAddAsync(Key, () => Task.FromResult("source"), Method);

        await saving.Cache.RemoveAsync(Key);
        other.Deliver(saving.Published.Single());
        other.RedisReturns("v2");

        Assert.Equal("v2", await other.Cache.GetOrAddAsync(Key, () => Task.FromResult("source"), Method));
    }

    [Fact]
    public async Task OwnMessage_IsIgnored()
    {
        var replica = new Replica();
        await replica.StartAsync();
        await replica.Cache.RemoveAsync(Key);
        replica.RedisReturns("v1");
        await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("source"), Method);

        replica.Deliver(replica.Published.Single());
        replica.RedisReturns("v2");

        Assert.Equal("v1", await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("source"), Method));
    }

    [Fact]
    public async Task ReadThatOverlapsARemoval_IsNotKeptInMemory()
    {
        var replica = new Replica();
        var readStarted = new TaskCompletionSource();
        var releaseRead = new TaskCompletionSource<string>();
        replica.Redis
            .GetOrAddAsync(Key, Arg.Any<Func<Task<string>>>(), Method, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                readStarted.TrySetResult();
                return releaseRead.Task;
            });

        var inFlight = replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("source"), Method);
        await readStarted.Task;
        await replica.Cache.RemoveAsync(Key);
        releaseRead.SetResult("old");
        await inFlight;

        replica.RedisReturns("new");
        Assert.Equal("new", await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("source"), Method));
    }

    [Fact]
    public async Task RemoveAsync_WhenPublishFails_StillDropsMemoryAndRedis()
    {
        var replica = new Replica(redisAvailable: false);
        replica.RedisReturns("v1");
        await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("source"), Method);

        await replica.Cache.RemoveAsync(Key);
        replica.RedisReturns("v2");

        await replica.Redis.Received(1).RemoveAsync(Key);
        Assert.Equal("v2", await replica.Cache.GetOrAddAsync(Key, () => Task.FromResult("source"), Method));
    }

    private sealed class Replica
    {
        private readonly IServiceProvider _services = Substitute.For<IServiceProvider>();
        private Action<RedisChannel, RedisValue>? _handler;

        public Replica(bool redisAvailable = true)
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
            _services.GetService(typeof(IConnectionMultiplexer)).Returns(_ => redisAvailable
                ? multiplexer
                : throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is down"));

            var settings = Options.Create(new CacheSettings
            {
                Memory = new MemoryCacheSettings { DefaultDurationInSeconds = 60 }
            });

            Cache = new TwoLevelCache(
                new MemoryCache(new MemoryCacheOptions()),
                Redis,
                settings,
                _services,
                NullLogger<TwoLevelCache>.Instance);
        }

        public IAdvancedRedisCacheService Redis { get; } = Substitute.For<IAdvancedRedisCacheService>();

        public TwoLevelCache Cache { get; }

        public List<RedisValue> Published { get; } = [];

        public TaskCompletionSource Subscribed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void RedisReturns(string value) =>
            Redis.GetOrAddAsync(Key, Arg.Any<Func<Task<string>>>(), Method, Arg.Any<CancellationToken>())
                .Returns(value);

        public async Task StartAsync()
        {
            await Cache.StartAsync(CancellationToken.None);
            await Subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void Deliver(RedisValue message) => _handler!(TwoLevelCache.Channel, message);
    }
}
