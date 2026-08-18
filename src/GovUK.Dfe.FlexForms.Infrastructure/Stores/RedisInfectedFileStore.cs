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
            var key = $"{FlexFormsCacheKeys.InfectedFilePrefix}{fileId}";
            return redis.GetDatabase().KeyExists(key);
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
            var key = $"{FlexFormsCacheKeys.InfectedFileNamePrefix}{applicationId}:{originalFileName}";
            return redis.GetDatabase().KeyExists(key);
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
}
