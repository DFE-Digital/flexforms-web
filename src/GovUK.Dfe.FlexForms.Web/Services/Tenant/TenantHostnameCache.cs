using System.Collections.Concurrent;

namespace GovUK.Dfe.FlexForms.Web.Services.Tenant;

/// <summary>
/// Process-wide hostname → tenant map. <see cref="TenantIdResolver"/> is scoped, so this
/// must live as a singleton or every request would call <c>GET /v1/tenant-config/resolve</c>.
/// </summary>
public sealed class TenantHostnameCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string hostname, DateTimeOffset utcNow, bool allowExpired, out Guid tenantId)
    {
        tenantId = default;
        if (!_entries.TryGetValue(hostname, out var entry))
        {
            return false;
        }

        if (!allowExpired && entry.ExpiresAt <= utcNow)
        {
            return false;
        }

        tenantId = entry.TenantId;
        return true;
    }

    public void Set(string hostname, Guid tenantId, DateTimeOffset expiresAt) =>
        _entries[hostname] = new CacheEntry(tenantId, expiresAt);

    public void Clear() => _entries.Clear();

    /// <summary>
    /// Coalesces concurrent lookups for the same hostname so a traffic spike does not
    /// stampede the platform API / Front Door.
    /// </summary>
    public async Task<T> RunSerializedAsync<T>(
        string hostname,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        var gate = _gates.GetOrAdd(hostname, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await action();
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed record CacheEntry(Guid TenantId, DateTimeOffset ExpiresAt);
}
