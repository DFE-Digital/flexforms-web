using GovUK.Dfe.FlexForms.Web.Services.Tenant;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services.Tenant;

public class TenantHostnameCacheTests
{
    [Fact]
    public void TryGet_ReturnsFreshEntry_AndNotExpired()
    {
        var cache = new TenantHostnameCache();
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        cache.Set("visits.example.gov.uk", tenantId, now.AddMinutes(10));

        Assert.True(cache.TryGet("visits.example.gov.uk", now, allowExpired: false, out var fresh));
        Assert.Equal(tenantId, fresh);
    }

    [Fact]
    public void TryGet_HidesExpired_UnlessAllowExpired()
    {
        var cache = new TenantHostnameCache();
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        cache.Set("visits.example.gov.uk", tenantId, now.AddMinutes(-1));

        Assert.False(cache.TryGet("visits.example.gov.uk", now, allowExpired: false, out _));
        Assert.True(cache.TryGet("visits.example.gov.uk", now, allowExpired: true, out var stale));
        Assert.Equal(tenantId, stale);
    }

    [Fact]
    public async Task RunSerializedAsync_RunsOneCallerAtATime()
    {
        var cache = new TenantHostnameCache();
        var running = 0;
        var maxRunning = 0;
        var started = new TaskCompletionSource();

        async Task<int> Work()
        {
            var current = Interlocked.Increment(ref running);
            Interlocked.Exchange(ref maxRunning, Math.Max(maxRunning, current));
            started.TrySetResult();
            await Task.Delay(50);
            Interlocked.Decrement(ref running);
            return current;
        }

        var first = cache.RunSerializedAsync("host", Work, CancellationToken.None);
        await started.Task;
        var second = cache.RunSerializedAsync("host", Work, CancellationToken.None);

        await Task.WhenAll(first, second);
        Assert.Equal(1, maxRunning);
    }
}
