using System.Text.Json;
using GovUK.Dfe.CoreLibs.Caching.Settings;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Infrastructure.Caching;
using GovUK.Dfe.FlexForms.Infrastructure.Parsers;
using GovUK.Dfe.FlexForms.Infrastructure.Providers;
using GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Providers;

public class FormTemplateProviderRedisTests
{
    private const string TemplateId = "6b1d3c2a-0f4e-4d7b-9a51-2e8c7f6a9b10";
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "transfer-template.json");

    private readonly InMemorySharedCacheStore _redis = new();
    private readonly ITemplateStore _store = Substitute.For<ITemplateStore>();

    public FormTemplateProviderRedisTests()
    {
        _store.GetTemplateStreamAsync(TemplateId, Arg.Any<CancellationToken>())
            .Returns(_ => File.OpenRead(FixturePath));
    }

    [Fact]
    public async Task RealTemplate_ReadByAnotherReplicaFromRedis_IsIdenticalToTheParsedTemplate()
    {
        var parsed = await Provider(redisAvailable: true).GetTemplateAsync(TemplateId);

        var fromRedis = await Provider(redisAvailable: true).GetTemplateAsync(TemplateId);

        await _store.Received(1).GetTemplateStreamAsync(TemplateId, Arg.Any<CancellationToken>());
        Assert.Equal(JsonSerializer.Serialize(parsed), JsonSerializer.Serialize(fromRedis));
    }

    [Fact]
    public async Task RedisUnavailable_LoadsTheTemplateFromTheApi()
    {
        var template = await Provider(redisAvailable: false).GetTemplateAsync(TemplateId);

        Assert.Equal("Transfer Applications", template.TemplateName);
        await _store.Received(1).GetTemplateStreamAsync(TemplateId, Arg.Any<CancellationToken>());
    }

    // Each call is a separate replica: its own memory, the shared store.
    private FormTemplateProvider Provider(bool redisAvailable)
    {
        var services = Substitute.For<IServiceProvider>();
        var unavailable = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is down");
        services.GetService(typeof(ISharedCacheStore)).Returns(_ => redisAvailable ? _redis : throw unavailable);
        services.GetService(typeof(IConnectionMultiplexer)).Returns(_ => throw unavailable);

        var cache = new TwoLevelCache(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new CacheSettings()),
            services,
            NullLogger<TwoLevelCache>.Instance);

        return new FormTemplateProvider(_store, new JsonFormTemplateParser(), cache);
    }
}
