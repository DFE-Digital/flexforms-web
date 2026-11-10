namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Short-lived in-memory copy on each Web replica, backed by one shared copy in Redis.
/// Durations come from <c>CacheSettings:Memory:Durations</c> and <c>CacheSettings:Redis:Durations</c>, keyed by method name.
/// </summary>
public interface ITwoLevelCache
{
    Task<T> GetOrAddAsync<T>(
        string cacheKey,
        Func<Task<T>> fetchFunction,
        string methodName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the shared copy and the in-memory copy on every replica. Call after the cached data changes.
    /// </summary>
    Task RemoveAsync(string cacheKey);

    /// <summary>True when the shared copy exists. For diagnostics; throws when Redis is unavailable.</summary>
    Task<bool> ContainsAsync(string cacheKey);
}
