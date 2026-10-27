using GovUK.Dfe.FlexForms.Application.Diagnostics;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Infrastructure.Stores;

/// <summary>
/// Redis adapter for the malware-scan file blacklist.
/// </summary>
public sealed class RedisInfectedFileStore(
    IConnectionMultiplexer redis,
    ILogger<RedisInfectedFileStore> logger) : IInfectedFileStore
{
    public bool IsFileInfected(Guid fileId)
    {
        try
        {
            using (RequestTimings.Measure("redis infected-file check"))
                return redis.GetDatabase().KeyExists(FileIdKey(fileId));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to check infected-file blacklist for {FileId}", fileId);
            return false;
        }
    }

    public bool IsFileNameInfected(string applicationId, string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(applicationId) || string.IsNullOrWhiteSpace(originalFileName))
            return false;

        try
        {
            using (RequestTimings.Measure("redis infected-file check"))
                return redis.GetDatabase().KeyExists(FileNameKey(applicationId, originalFileName));
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to check infected-filename blacklist for {ApplicationId}/{FileName}",
                applicationId,
                originalFileName);
            return false;
        }
    }

    public IReadOnlySet<Guid> FindInfectedFiles(string? applicationId, IReadOnlyCollection<InfectedFileCandidate> files)
    {
        if (files.Count == 0)
            return new HashSet<Guid>();

        try
        {
            using var timing = RequestTimings.Measure("redis infected-file check");

            var batch = redis.GetDatabase().CreateBatch();
            var lookups = new List<(Guid FileId, Task<bool> Exists)>();
            foreach (var file in files)
            {
                lookups.Add((file.FileId, batch.KeyExistsAsync(FileIdKey(file.FileId))));
                if (!string.IsNullOrWhiteSpace(applicationId) && !string.IsNullOrWhiteSpace(file.OriginalFileName))
                    lookups.Add((file.FileId, batch.KeyExistsAsync(FileNameKey(applicationId, file.OriginalFileName))));
            }

            batch.Execute();
            Task.WhenAll(lookups.Select(l => l.Exists)).GetAwaiter().GetResult();

            return lookups.Where(l => l.Exists.Result).Select(l => l.FileId).ToHashSet();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to check infected-file blacklist for {Count} file(s)", files.Count);
            return new HashSet<Guid>();
        }
    }

    private static string FileIdKey(Guid fileId) => $"{FlexFormsCacheKeys.InfectedFilePrefix}{fileId}";

    private static string FileNameKey(string applicationId, string originalFileName) =>
        $"{FlexFormsCacheKeys.InfectedFileNamePrefix}{applicationId}:{originalFileName}";
}
