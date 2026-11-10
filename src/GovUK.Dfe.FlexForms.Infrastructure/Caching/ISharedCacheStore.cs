namespace GovUK.Dfe.FlexForms.Infrastructure.Caching;

/// <summary>
/// The shared (Redis) level of <see cref="TwoLevelCache"/>. Every key has a version that invalidation increments,
/// so a fill that read the version before an invalidation cannot write its (possibly old) value afterwards.
/// </summary>
public interface ISharedCacheStore
{
    /// <summary>Returns the stored value, if any, and the key's current version (0 when it has none).</summary>
    Task<SharedCacheEntry> ReadAsync(string cacheKey);

    /// <summary>Stores the value only if the key's version is still <paramref name="expectedVersion"/>.</summary>
    Task<bool> WriteIfUnchangedAsync(string cacheKey, byte[] value, long expectedVersion, TimeSpan expiry);

    /// <summary>Increments the key's version and deletes the stored value.</summary>
    Task InvalidateAsync(string cacheKey);
}

public readonly record struct SharedCacheEntry(byte[]? Value, long Version);
