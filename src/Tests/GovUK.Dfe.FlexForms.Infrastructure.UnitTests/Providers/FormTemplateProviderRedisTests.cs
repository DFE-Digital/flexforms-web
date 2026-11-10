using System.Text.Json;
using GovUK.Dfe.CoreLibs.Caching.Services;
using GovUK.Dfe.CoreLibs.Caching.Settings;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Infrastructure.Parsers;
using GovUK.Dfe.FlexForms.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.Extensions;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Providers;

public class FormTemplateProviderRedisTests
{
    private const string TemplateId = "6b1d3c2a-0f4e-4d7b-9a51-2e8c7f6a9b10";
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "transfer-template.json");

    private readonly IDatabase _database = Substitute.For<IDatabase>();
    private readonly ITemplateStore _store = Substitute.For<ITemplateStore>();
    private readonly FormTemplateProvider _provider;

    public FormTemplateProviderRedisTests()
    {
        _database.ReturnsForAll(Task.FromResult(true));
        _database.ReturnsForAll(Task.FromResult(RedisResult.Create((RedisValue)1)));
        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(RedisValue.Null);

        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_database);

        var settings = Options.Create(new CacheSettings
        {
            Redis = new RedisCacheSettings { KeyPrefix = "FlexForms:Cache:", DefaultDurationInSeconds = 300 }
        });
        var cache = new RedisCacheService(redis, NullLogger<RedisCacheService>.Instance, settings);

        _store.GetTemplateStreamAsync(TemplateId, Arg.Any<CancellationToken>())
            .Returns(_ => File.OpenRead(FixturePath));

        _provider = new FormTemplateProvider(_store, new JsonFormTemplateParser(), new RedisOnlyCache(cache));
    }

    [Fact]
    public async Task RealTemplate_ReadBackFromRedis_IsIdenticalToTheParsedTemplate()
    {
        var parsed = await _provider.GetTemplateAsync(TemplateId);
        var stored = StoredValue();

        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(stored);
        var fromRedis = await _provider.GetTemplateAsync(TemplateId);

        await _store.Received(1).GetTemplateStreamAsync(TemplateId, Arg.Any<CancellationToken>());
        Assert.Equal(JsonSerializer.Serialize(parsed), JsonSerializer.Serialize(fromRedis));
    }

    [Fact]
    public async Task RedisUnavailable_LoadsTheTemplateFromTheApi()
    {
        _database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns<Task<RedisValue>>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is down"));

        var template = await _provider.GetTemplateAsync(TemplateId);

        Assert.Equal("Transfer Applications", template.TemplateName);
        await _store.Received(1).GetTemplateStreamAsync(TemplateId, Arg.Any<CancellationToken>());
    }

    private RedisValue StoredValue() =>
        _database.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IDatabase.StringSetAsync))
            .Select(call => call.GetArguments())
            .Where(args => args[0] is RedisKey key && key.ToString().StartsWith("FlexForms:Cache:FormTemplate_")
                && !key.ToString().EndsWith(":lock"))
            .Select(args => (RedisValue)args[1]!)
            .Single();

    /// <summary>The Redis level of the Web two-level cache, so the template goes through the real Redis serialiser.</summary>
    private sealed class RedisOnlyCache(RedisCacheService redis) : ITwoLevelCache
    {
        public Task<T> GetOrAddAsync<T>(string cacheKey, Func<Task<T>> fetchFunction, string methodName,
            CancellationToken cancellationToken = default) =>
            redis.GetOrAddAsync(cacheKey, fetchFunction, methodName, cancellationToken);

        public Task RemoveAsync(string cacheKey) => redis.RemoveAsync(cacheKey);
    }
}
