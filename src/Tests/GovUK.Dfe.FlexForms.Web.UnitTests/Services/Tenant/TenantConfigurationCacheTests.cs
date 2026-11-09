using System.Collections.Concurrent;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services.Tenant;

public class TenantConfigurationCacheTests
{
    private readonly ConcurrentDictionary<string, RedisValue> _versions = new();
    private readonly IDatabase _database = Substitute.For<IDatabase>();
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly Guid _tenantId = Guid.NewGuid();

    public TenantConfigurationCacheTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(call => Task.FromResult(_versions.GetValueOrDefault(call.Arg<RedisKey>().ToString())));
        _database.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(call =>
            {
                var key = call.Arg<RedisKey>().ToString();
                var value = call.Arg<RedisValue>();
                if (call.Arg<When>() == When.NotExists)
                    return Task.FromResult(_versions.TryAdd(key, value));

                _versions[key] = value;
                return Task.FromResult(true);
            });
    }

    [Fact]
    public async Task GetOrAddAsync_ReusesLocalValue_WhenSharedVersionIsUnchanged()
    {
        var cache = CreateCache();
        var expected = Configuration(_tenantId, "original");
        var calls = 0;
        Task<PlatformTenantConfigurationResponse> Load(CancellationToken _)
        {
            calls++;
            return Task.FromResult(expected);
        }

        Assert.Same(expected, await cache.GetOrAddAsync(_tenantId, Load));
        Assert.Same(expected, await cache.GetOrAddAsync(_tenantId, Load));
        Assert.Equal(1, calls);
        await _database.Received(3).StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task InvalidateAsync_RefreshesBothReplicas_AndPreservesOtherTenants()
    {
        var first = CreateCache();
        var second = CreateCache();
        var otherTenant = Guid.NewGuid();
        var original = Configuration(_tenantId, "original");
        var updated = Configuration(_tenantId, "updated");
        var other = Configuration(otherTenant, "other");
        await first.GetOrAddAsync(_tenantId, _ => Task.FromResult(original));
        await second.GetOrAddAsync(_tenantId, _ => Task.FromResult(original));
        await second.GetOrAddAsync(otherTenant, _ => Task.FromResult(other));

        await first.InvalidateAsync(_tenantId);

        Assert.Same(updated, await first.GetOrAddAsync(_tenantId, _ => Task.FromResult(updated)));
        Assert.Same(updated, await second.GetOrAddAsync(_tenantId, _ => Task.FromResult(updated)));
        Assert.Same(other, await second.GetOrAddAsync(otherTenant,
            _ => throw new InvalidOperationException("Unrelated tenant must remain cached.")));
    }

    [Fact]
    public async Task GetOrAddAsync_Reloads_WhenOtherReplicaInvalidatesDuringLoad()
    {
        var savingReplica = CreateCache();
        var loadingReplica = CreateCache();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<PlatformTenantConfigurationResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var updated = Configuration(_tenantId, "updated");
        var calls = 0;
        Task<PlatformTenantConfigurationResponse> Load(CancellationToken _)
        {
            if (++calls == 1)
            {
                started.SetResult();
                return pending.Task;
            }
            return Task.FromResult(updated);
        }

        var result = loadingReplica.GetOrAddAsync(_tenantId, Load);
        await started.Task;
        await savingReplica.InvalidateAsync(_tenantId);
        pending.SetResult(Configuration(_tenantId, "stale"));

        Assert.Same(updated, await result);
        Assert.Equal(2, calls);
        Assert.Same(updated, await loadingReplica.GetOrAddAsync(_tenantId,
            _ => throw new InvalidOperationException("Updated result must be cached.")));
    }

    [Fact]
    public async Task GetOrAddAsync_DoesNotReturnInvalidatedValue_WhenPlatformLoadFails()
    {
        var first = CreateCache();
        var second = CreateCache();
        await second.GetOrAddAsync(_tenantId, _ => Task.FromResult(Configuration(_tenantId, "original")));
        await first.InvalidateAsync(_tenantId);

        await Assert.ThrowsAsync<HttpRequestException>(() => second.GetOrAddAsync(_tenantId,
            _ => Task.FromException<PlatformTenantConfigurationResponse>(new HttpRequestException("API unavailable"))));
    }

    [Fact]
    public async Task GetOrAddAsync_Reloads_WhenSharedMarkerIsRemoved()
    {
        var cache = CreateCache();
        await cache.GetOrAddAsync(_tenantId, _ => Task.FromResult(Configuration(_tenantId, "original")));
        _versions.Clear();
        var updated = Configuration(_tenantId, "updated");

        Assert.Same(updated, await cache.GetOrAddAsync(_tenantId, _ => Task.FromResult(updated)));
    }

    [Fact]
    public async Task GetOrAddAsync_FailsLoud_WhenRedisReadFails_EvenWithWarmLocalCache()
    {
        var cache = CreateCache();
        await cache.GetOrAddAsync(_tenantId, _ => Task.FromResult(Configuration(_tenantId, "original")));
        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromException<RedisValue>(new RedisException("Redis unavailable")));

        await Assert.ThrowsAsync<RedisException>(() => cache.GetOrAddAsync(_tenantId,
            _ => throw new InvalidOperationException("Must not bypass Redis.")));
    }

    [Fact]
    public async Task InvalidateAsync_FailsLoud_WhenRedisWriteFails()
    {
        _database.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromException<bool>(new RedisException("Redis unavailable")));

        await Assert.ThrowsAsync<RedisException>(() => CreateCache().InvalidateAsync(_tenantId));
    }

    [Fact]
    public async Task InvalidateAsync_FailsLoud_WhenRedisRejectsWrite()
    {
        _database.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(false));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateCache().InvalidateAsync(_tenantId));
    }

    [Fact]
    public async Task GetOrAddAsync_UsesWebSpecificTenantVersionKey_WithoutStoringConfiguration()
    {
        await CreateCache().GetOrAddAsync(_tenantId,
            _ => Task.FromResult(Configuration(_tenantId, "original")));

        var version = Assert.Single(_versions);
        Assert.Equal($"{FlexFormsCacheKeys.WebTenantConfigurationVersionPrefix}{_tenantId:D}", version.Key);
        Assert.True(Guid.TryParseExact(version.Value.ToString(), "N", out _));
    }

    [Fact]
    public async Task CancelledOperations_DoNotReadOrWriteRedis()
    {
        var cache = CreateCache();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetOrAddAsync(_tenantId,
            _ => Task.FromResult(Configuration(_tenantId, "original")), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.InvalidateAsync(_tenantId, cancellation.Token));
        Assert.Empty(_database.ReceivedCalls());
    }

    private TenantConfigurationCache CreateCache() =>
        new(Options.Create(new PlatformBootstrapOptions { TenantConfigurationCacheMinutes = 10 }),
            _redis, NullLogger<TenantConfigurationCache>.Instance);

    private static PlatformTenantConfigurationResponse Configuration(Guid tenantId, string heading) =>
        new(tenantId, "Test tenant", "Web", DateTime.UtcNow,
            new Dictionary<string, string?> { ["ApplicationTerminology:Plural"] = heading });
}
