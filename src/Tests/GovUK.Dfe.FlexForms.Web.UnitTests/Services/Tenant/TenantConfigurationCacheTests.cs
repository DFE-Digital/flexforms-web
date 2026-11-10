using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services.Tenant;

public class TenantConfigurationCacheTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task GetOrAddAsync_ReturnsCachedValue_WithoutCallingFactoryAgain()
    {
        var cache = CreateCache();
        var calls = 0;

        await cache.GetOrAddAsync(_tenantId, _ => Load(ref calls, "first"));
        var second = await cache.GetOrAddAsync(_tenantId, _ => Load(ref calls, "second"));

        Assert.Equal("first", second.TenantName);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Invalidate_MakesNextCallReload()
    {
        var cache = CreateCache();
        var calls = 0;
        await cache.GetOrAddAsync(_tenantId, _ => Load(ref calls, "old"));

        cache.Invalidate(_tenantId);
        var reloaded = await cache.GetOrAddAsync(_tenantId, _ => Load(ref calls, "new"));

        Assert.Equal("new", reloaded.TenantName);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Invalidate_DuringLoad_DoesNotCacheTheInFlightResult()
    {
        var cache = CreateCache();
        var loadStarted = new TaskCompletionSource();
        var releaseLoad = new TaskCompletionSource<PlatformTenantConfigurationResponse>();

        var inFlight = cache.GetOrAddAsync(_tenantId, _ =>
        {
            loadStarted.SetResult();
            return releaseLoad.Task;
        });
        await loadStarted.Task;

        cache.Invalidate(_tenantId);
        releaseLoad.SetResult(Response("loaded before the change"));
        var inFlightResult = await inFlight;

        var calls = 0;
        var next = await cache.GetOrAddAsync(_tenantId, _ => Load(ref calls, "loaded after the change"));

        Assert.Equal("loaded before the change", inFlightResult.TenantName);
        Assert.Equal("loaded after the change", next.TenantName);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Invalidate_OnlyAffectsTheGivenTenant()
    {
        var cache = CreateCache();
        var otherTenant = Guid.NewGuid();
        var calls = 0;
        await cache.GetOrAddAsync(_tenantId, _ => Load(ref calls, "mine"));
        await cache.GetOrAddAsync(otherTenant, _ => Load(ref calls, "other"));

        cache.Invalidate(_tenantId);
        var other = await cache.GetOrAddAsync(otherTenant, _ => Load(ref calls, "reloaded"));

        Assert.Equal("other", other.TenantName);
        Assert.Equal(2, calls);
    }

    private static TenantConfigurationCache CreateCache() =>
        new(Options.Create(new PlatformBootstrapOptions { TenantConfigurationCacheMinutes = 10 }));

    private Task<PlatformTenantConfigurationResponse> Load(ref int calls, string name)
    {
        calls++;
        return Task.FromResult(Response(name));
    }

    private PlatformTenantConfigurationResponse Response(string name) =>
        new(_tenantId, name, "Web", DateTime.UtcNow, new Dictionary<string, string?>());
}
