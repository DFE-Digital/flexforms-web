using GovUK.Dfe.FlexForms.Infrastructure.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Caching;

public class ReplicaBroadcastServiceTests
{
    private static readonly RedisChannel Channel = RedisChannel.Literal("FlexForms:Test");

    private readonly ISubscriber _subscriber = Substitute.For<ISubscriber>();
    private readonly IServiceProvider _services = Substitute.For<IServiceProvider>();
    private readonly List<RedisValue> _published = [];
    private volatile bool _redisAvailable = true;

    public ReplicaBroadcastServiceTests()
    {
        _subscriber
            .PublishAsync(Channel, Arg.Do<RedisValue>(_published.Add), Arg.Any<CommandFlags>())
            .Returns(1L);
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetSubscriber(Arg.Any<object?>()).Returns(_subscriber);
        _services.GetService(typeof(IConnectionMultiplexer)).Returns(_ => _redisAvailable
            ? multiplexer
            : throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is down"));
    }

    [Fact]
    public async Task Publish_SendsPayloadWithThisInstancesId()
    {
        var service = CreateService();

        await service.Publish("tenant-1");

        Assert.Matches(@"^tenant-1\|[0-9a-f]{32}$", Assert.Single(_published).ToString());
    }

    [Fact]
    public async Task Publish_WhenRedisIsUnavailable_DoesNotThrow()
    {
        _redisAvailable = false;

        await CreateService().Publish("tenant-1");

        Assert.Empty(_published);
    }

    [Fact]
    public async Task MessageFromAnotherInstance_IsApplied_OwnMessageIsNot()
    {
        var service = CreateService();
        await service.Publish("mine");

        service.OnMessage(_published.Single());
        service.OnMessage("theirs|0123456789abcdef0123456789abcdef");

        Assert.Equal(new[] { "theirs" }, service.Received);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-separator")]
    [InlineData("|instance")]
    [InlineData("payload|")]
    public void UnreadableMessage_IsIgnored(string message)
    {
        var service = CreateService();

        service.OnMessage(message);

        Assert.Empty(service.Received);
    }

    [Fact]
    public void FailureWhileApplyingAMessage_IsSwallowed()
    {
        var service = CreateService();
        service.ThrowOnMessage = true;

        service.OnMessage("payload|0123456789abcdef0123456789abcdef");

        Assert.Equal(new[] { "payload" }, service.Received);
    }

    [Fact]
    public async Task Subscription_IsRetried_UntilRedisIsAvailable()
    {
        _redisAvailable = false;
        var subscribed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _subscriber
            .SubscribeAsync(Channel, Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>())
            .Returns(_ =>
            {
                subscribed.TrySetResult();
                return Task.CompletedTask;
            });
        var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        _redisAvailable = true;

        await subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StoppingWhileWaitingToRetry_EndsTheService()
    {
        var connectAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _services.GetService(typeof(IConnectionMultiplexer)).Returns(_ =>
        {
            connectAttempted.TrySetResult();
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is down");
        });
        var service = new TestBroadcastService(Channel, _services) { SubscribeRetryDelay = TimeSpan.FromMinutes(5) };

        await service.StartAsync(CancellationToken.None);
        await connectAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }

    private TestBroadcastService CreateService() =>
        new(Channel, _services) { SubscribeRetryDelay = TimeSpan.FromMilliseconds(10) };

    private sealed class TestBroadcastService(RedisChannel channel, IServiceProvider services)
        : ReplicaBroadcastService(channel, services, NullLogger.Instance)
    {
        public List<string> Received { get; } = [];

        public bool ThrowOnMessage { get; set; }

        public Task Publish(string payload) => PublishAsync(payload);

        protected override void OnRemoteMessage(string payload)
        {
            Received.Add(payload);
            if (ThrowOnMessage)
                throw new InvalidOperationException("Could not apply");
        }
    }
}
