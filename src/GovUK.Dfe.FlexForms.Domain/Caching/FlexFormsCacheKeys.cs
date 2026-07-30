namespace GovUK.Dfe.FlexForms.Domain.Caching;

/// <summary>
/// Redis / cache key prefixes for FlexForms.
/// Must differ from legacy EAT (<c>DfE:Cache:</c>) when sharing an Azure Redis instance.
/// </summary>
public static class FlexFormsCacheKeys
{
    /// <summary>Applied by CoreLibs <c>CacheSettings:Redis:KeyPrefix</c> to all hybrid-cache keys.</summary>
    public const string RedisKeyPrefix = "FlexForms:Cache:";

    /// <summary>Notification service keys (CoreLibs NotificationService:RedisKeyPrefix).</summary>
    public const string NotificationsKeyPrefix = "FlexForms:notifications:";

    /// <summary>Raw Redis blacklist for infected file ids (bypasses KeyPrefix).</summary>
    public const string InfectedFilePrefix = "FlexForms:InfectedFile:";

    /// <summary>Raw Redis blacklist by application + original file name.</summary>
    public const string InfectedFileNamePrefix = "FlexForms:InfectedFileName:";
}
