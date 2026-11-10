namespace GovUK.Dfe.FlexForms.Infrastructure.Caching;

/// <summary>
/// The shared (Redis) level of <see cref="TwoLevelCache"/>. Every invalidation gives the key a new random version,
/// so a fill that read the version before an invalidation cannot write its (possibly old) value afterwards. Versions
/// never repeat, so a version expiring and being recreated cannot match a version a fill already saw.
/// </summary>
public interface ISharedCacheStore
{
    /// <summary>Returns the stored value, if any, and the key's current version (null when it has none).</summary>
    Task<SharedCacheEntry> ReadAsync(string cacheKey);

    /// <summary>Stores the value only if the key's version is still <paramref name="expectedVersion"/>.</summary>
    Task<bool> WriteIfUnchangedAsync(string cacheKey, byte[] value, string? expectedVersion, TimeSpan expiry);

    /// <summary>Gives the key a new version and deletes the stored value.</summary>
    Task InvalidateAsync(string cacheKey);
}

public readonly record struct SharedCacheEntry(byte[]? Value, string? Version);
