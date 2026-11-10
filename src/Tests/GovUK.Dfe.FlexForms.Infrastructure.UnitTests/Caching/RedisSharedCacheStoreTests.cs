using System.Text;
using GovUK.Dfe.CoreLibs.Caching.Settings;
using GovUK.Dfe.FlexForms.Infrastructure.Caching;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Caching;

public class RedisSharedCacheStoreTests
{
    private const string Key = "FormTemplate_abc";
    private const int ConfiguredDatabase = 2;

    private static readonly RedisKey[] ExpectedKeys =
        ["FlexForms:Cache:{FormTemplate_abc}", "FlexForms:Cache:{FormTemplate_abc}:version"];

    private readonly IDatabase _database = Substitute.For<IDatabase>();
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly RedisSharedCacheStore _store;

    public RedisSharedCacheStoreTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_database);
        _store = new RedisSharedCacheStore(
            _redis,
            Options.Create(new CacheSettings { Redis = new RedisCacheSettings { Database = ConfiguredDatabase } }));
    }

    [Fact]
    public async Task ReadAsync_ReturnsValueAndVersion()
    {
        ScriptReturns(RedisResult.Create(
        [
            RedisResult.Create((RedisValue)Encoding.UTF8.GetBytes("payload")),
            RedisResult.Create((RedisValue)"v-3")
        ]));

        var entry = await _store.ReadAsync(Key);

        Assert.Equal("payload", Encoding.UTF8.GetString(entry.Value!));
        Assert.Equal("v-3", entry.Version);
        _redis.Received().GetDatabase(ConfiguredDatabase, Arg.Any<object?>());
        await _database.Received(1).ScriptEvaluateAsync(
            Arg.Any<string>(),
            Arg.Is<RedisKey[]?>(keys => keys!.SequenceEqual(ExpectedKeys)),
            Arg.Any<RedisValue[]?>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ReadAsync_MissingValueAndVersion_AreNull()
    {
        ScriptReturns(RedisResult.Create(
        [
            RedisResult.Create(RedisValue.Null),
            RedisResult.Create(RedisValue.Null)
        ]));

        var entry = await _store.ReadAsync(Key);

        Assert.Null(entry.Value);
        Assert.Null(entry.Version);
    }

    [Theory]
    [InlineData(1L, true)]
    [InlineData(0L, false)]
    public async Task WriteIfUnchangedAsync_ReportsWhetherTheScriptWrote(long scriptResult, bool expected)
    {
        ScriptReturns(RedisResult.Create((RedisValue)scriptResult));
        var value = Encoding.UTF8.GetBytes("payload");

        var written = await _store.WriteIfUnchangedAsync(Key, value, "v-5", TimeSpan.FromMinutes(2));

        Assert.Equal(expected, written);
        await _database.Received(1).ScriptEvaluateAsync(
            Arg.Any<string>(),
            Arg.Is<RedisKey[]?>(keys => keys!.SequenceEqual(ExpectedKeys)),
            Arg.Is<RedisValue[]?>(args => args![0] == "v-5"
                && ((byte[])args[1]!).SequenceEqual(value)
                && args[2] == 120_000),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task WriteIfUnchangedAsync_NoVersionIsSentAsEmpty()
    {
        ScriptReturns(RedisResult.Create((RedisValue)1));

        await _store.WriteIfUnchangedAsync(Key, [1], null, TimeSpan.FromMinutes(2));

        await _database.Received(1).ScriptEvaluateAsync(
            Arg.Any<string>(),
            Arg.Any<RedisKey[]?>(),
            Arg.Is<RedisValue[]?>(args => args![0] == string.Empty),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task InvalidateAsync_SetsANewRandomVersionEachTime_ForOneDay()
    {
        ScriptReturns(RedisResult.Create((RedisValue)1));
        var versions = new List<string>();
        _database
            .When(db => db.ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Is<RedisKey[]?>(keys => keys!.SequenceEqual(ExpectedKeys)),
                Arg.Any<RedisValue[]?>(),
                Arg.Any<CommandFlags>()))
            .Do(call =>
            {
                var args = call.ArgAt<RedisValue[]?>(2)!;
                versions.Add(args[0].ToString());
                Assert.Equal((long)TimeSpan.FromDays(1).TotalMilliseconds, (long)args[1]);
            });

        await _store.InvalidateAsync(Key);
        await _store.InvalidateAsync(Key);

        Assert.Equal(2, versions.Count);
        Assert.All(versions, version => Assert.Matches("^[0-9a-f]{32}$", version));
        Assert.NotEqual(versions[0], versions[1]);
    }

    private void ScriptReturns(RedisResult result) =>
        _database
            .ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]?>(), Arg.Any<RedisValue[]?>(), Arg.Any<CommandFlags>())
            .Returns(result);
}
