using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Infrastructure.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Stores;

public class RedisInfectedFileStoreTests
{
    [Fact]
    public void IsFileInfected_returns_true_when_blacklist_key_exists()
    {
        var fileId = Guid.NewGuid();
        var database = Substitute.For<IDatabase>();
        database.KeyExists($"{FlexFormsCacheKeys.InfectedFilePrefix}{fileId}", Arg.Any<CommandFlags>()).Returns(true);

        var store = CreateStore(database);

        Assert.True(store.IsFileInfected(fileId));
    }

    [Fact]
    public void IsFileNameInfected_returns_true_when_filename_blacklist_key_exists()
    {
        const string applicationId = "app-1";
        const string fileName = "malware.exe";
        var database = Substitute.For<IDatabase>();
        database.KeyExists($"{FlexFormsCacheKeys.InfectedFileNamePrefix}{applicationId}:{fileName}", Arg.Any<CommandFlags>()).Returns(true);

        var store = CreateStore(database);

        Assert.True(store.IsFileNameInfected(applicationId, fileName));
    }

    [Fact]
    public void IsFileNameInfected_returns_false_when_application_or_filename_is_missing()
    {
        var store = CreateStore(Substitute.For<IDatabase>());

        Assert.False(store.IsFileNameInfected("", "file.pdf"));
        Assert.False(store.IsFileNameInfected("app-1", ""));
    }

    [Fact]
    public void FindInfectedFiles_checks_ids_and_names_in_one_batch()
    {
        var byId = Guid.NewGuid();
        var byName = Guid.NewGuid();
        var clean = Guid.NewGuid();
        var batch = Substitute.For<IBatch>();
        batch.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(Task.FromResult(false));
        batch.KeyExistsAsync($"{FlexFormsCacheKeys.InfectedFilePrefix}{byId}", Arg.Any<CommandFlags>()).Returns(Task.FromResult(true));
        batch.KeyExistsAsync($"{FlexFormsCacheKeys.InfectedFileNamePrefix}app-1:bad.pdf", Arg.Any<CommandFlags>()).Returns(Task.FromResult(true));
        var database = Substitute.For<IDatabase>();
        database.CreateBatch(Arg.Any<object?>()).Returns(batch);

        var infected = CreateStore(database).FindInfectedFiles("app-1",
        [
            new(byId, "a.pdf"),
            new(byName, "bad.pdf"),
            new(clean, "good.pdf")
        ]);

        Assert.Equal(new HashSet<Guid> { byId, byName }, infected);
        batch.Received(1).Execute();
        batch.ReceivedWithAnyArgs(6).KeyExistsAsync(default(RedisKey));
        database.DidNotReceiveWithAnyArgs().KeyExists(default(RedisKey));
    }

    [Fact]
    public void FindInfectedFiles_skips_name_checks_without_application_id()
    {
        var batch = Substitute.For<IBatch>();
        batch.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(Task.FromResult(false));
        var database = Substitute.For<IDatabase>();
        database.CreateBatch(Arg.Any<object?>()).Returns(batch);

        var infected = CreateStore(database).FindInfectedFiles(null, [new(Guid.NewGuid(), "a.pdf")]);

        Assert.Empty(infected);
        batch.ReceivedWithAnyArgs(1).KeyExistsAsync(default(RedisKey));
    }

    private static RedisInfectedFileStore CreateStore(IDatabase database)
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().Returns(database);
        return new RedisInfectedFileStore(redis, NullLogger<RedisInfectedFileStore>.Instance);
    }
}
