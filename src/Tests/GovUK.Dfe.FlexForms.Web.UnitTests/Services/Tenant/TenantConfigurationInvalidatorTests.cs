using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services.Tenant;

public class TenantConfigurationInvalidatorTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task InvalidateAsync_ClearsLocalCaches_AndPublishesTenantId()
    {
        var replica = new Replica(_tenantId);
        await replica.WarmAsync();

        await replica.Invalidator.InvalidateAsync(_tenantId);

        Assert.False(replica.HasCachedConfiguration());
        Assert.False(replica.HasCachedHostname());
        Assert.Single(replica.Published);
        Assert.StartsWith(_tenantId.ToString("D"), replica.Published[0].ToString());
    }

    [Fact]
    public async Task MessageFromAnotherReplica_ClearsThisReplicasCaches()
    {
        var saving = new Replica(_tenantId);
        var other = new Replica(_tenantId);
        await other.StartAsync();
        await other.WarmAsync();

        await saving.Invalidator.InvalidateAsync(_tenantId);
        other.Deliver(saving.Published.Single());

        Assert.False(other.HasCachedConfiguration());
        Assert.False(other.HasCachedHostname());
    }

    [Fact]
    public async Task OwnMessage_IsIgnored()
    {
        var replica = new Replica(_tenantId);
        await replica.StartAsync();
        await replica.Invalidator.InvalidateAsync(_tenantId);
        await replica.WarmAsync();

        replica.Deliver(replica.Published.Single());

        Assert.True(replica.HasCachedConfiguration());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid|instance")]
    [InlineData("no-separator")]
    public async Task UnreadableMessage_IsIgnored(string message)
    {
        var replica = new Replica(_tenantId);
        await replica.StartAsync();
        await replica.WarmAsync();

        replica.Deliver(message);

        Assert.True(replica.HasCachedConfiguration());
    }

    [Fact]
    public async Task InvalidateAsync_WhenRedisIsUnavailable_StillClearsLocalCaches()
    {
        var replica = new Replica(_tenantId, redisAvailable: false);
        await replica.WarmAsync();

        await replica.Invalidator.InvalidateAsync(_tenantId);

        Assert.False(replica.HasCachedConfiguration());
        Assert.False(replica.HasCachedHostname());
    }

    [Fact]
    public async Task ExecuteAsync_RetriesSubscribe_UntilRedisIsAvailable()
    {
        var replica = new Replica(_tenantId, redisAvailable: false);
        await replica.Invalidator.StartAsync(CancellationToken.None);

        replica.MakeRedisAvailable();

        await replica.Subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await replica.Invalidator.StopAsync(CancellationToken.None);
    }

    private sealed class Replica
    {
        private const string Hostname = "visits.example.gov.uk";
        private readonly Guid _tenantId;
        private readonly TenantConfigurationCache _cache;
        private readonly TenantHostnameCache _hostnames = new();
        private readonly IServiceProvider _services = Substitute.For<IServiceProvider>();
        private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
        private Action<RedisChannel, RedisValue>? _handler;
        private volatile bool _redisAvailable;

        public Replica(Guid tenantId, bool redisAvailable = true)
        {
            _tenantId = tenantId;
            _cache = new TenantConfigurationCache(
                Options.Create(new PlatformBootstrapOptions { TenantConfigurationCacheMinutes = 10 }));

            var subscriber = Substitute.For<ISubscriber>();
            subscriber
                .PublishAsync(TenantConfigurationInvalidator.Channel, Arg.Do<RedisValue>(Published.Add), Arg.Any<CommandFlags>())
                .Returns(1L);
            subscriber
                .SubscribeAsync(
                    TenantConfigurationInvalidator.Channel,
                    Arg.Do<Action<RedisChannel, RedisValue>>(handler =>
                    {
                        _handler = handler;
                        Subscribed.TrySetResult();
                    }),
                    Arg.Any<CommandFlags>())
                .Returns(Task.CompletedTask);
            _redis.GetSubscriber(Arg.Any<object?>()).Returns(subscriber);

            _redisAvailable = redisAvailable;
            _services.GetService(typeof(IConnectionMultiplexer)).Returns(_ => _redisAvailable
                ? _redis
                : throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is down"));

            Invalidator = new TenantConfigurationInvalidator(
                _cache,
                _hostnames,
                _services,
                NullLogger<TenantConfigurationInvalidator>.Instance)
            {
                SubscribeRetryDelay = TimeSpan.FromMilliseconds(10)
            };
        }

        public TenantConfigurationInvalidator Invalidator { get; }

        public List<RedisValue> Published { get; } = [];

        public TaskCompletionSource Subscribed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void MakeRedisAvailable() => _redisAvailable = true;

        public async Task StartAsync()
        {
            await Invalidator.StartAsync(CancellationToken.None);
            await Subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public async Task WarmAsync()
        {
            await _cache.GetOrAddAsync(_tenantId, _ => Task.FromResult(
                new PlatformTenantConfigurationResponse(
                    _tenantId, "Visits", "Web", DateTime.UtcNow, new Dictionary<string, string?>())));
            _hostnames.Set(Hostname, _tenantId, DateTimeOffset.UtcNow.AddMinutes(10));
        }

        public void Deliver(RedisValue message) =>
            _handler!(TenantConfigurationInvalidator.Channel, message);

        public bool HasCachedConfiguration()
        {
            var reloaded = false;
            _cache.GetOrAddAsync(_tenantId, _ =>
            {
                reloaded = true;
                return Task.FromResult(new PlatformTenantConfigurationResponse(
                    _tenantId, "Visits", "Web", DateTime.UtcNow, new Dictionary<string, string?>()));
            }).GetAwaiter().GetResult();
            return !reloaded;
        }

        public bool HasCachedHostname() =>
            _hostnames.TryGet(Hostname, DateTimeOffset.UtcNow, allowExpired: false, out _);
    }
}
