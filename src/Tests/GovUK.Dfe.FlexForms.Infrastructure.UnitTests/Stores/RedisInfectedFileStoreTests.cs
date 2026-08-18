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

    private static RedisInfectedFileStore CreateStore(IDatabase database)
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().Returns(database);
        return new RedisInfectedFileStore(redis, NullLogger<RedisInfectedFileStore>.Instance);
    }
}
