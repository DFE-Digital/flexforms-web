using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Stores cookie authentication tickets server-side in Redis (via <see cref="IDistributedCache"/>).
/// Tickets must live in distributed cache so logout / FLUSHDB / multi-instance deploys stay consistent.
/// </summary>
public sealed class DistributedCacheTicketStore(
    IDistributedCache cache,
    ILogger<DistributedCacheTicketStore> logger) : ITicketStore
{
    private const string KeyPrefix = "FlexForms:AuthTicket:";

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = KeyPrefix + Guid.NewGuid().ToString("N");
        await RenewAsync(key, ticket);

        logger.LogDebug(
            "Stored new auth ticket with key {Key}. Expires: {Expires}",
            key,
            ticket.Properties?.ExpiresUtc);

        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var data = TicketSerializer.Default.Serialize(ticket);
        var expires = ticket.Properties?.ExpiresUtc ?? DateTimeOffset.UtcNow.AddHours(8);
        var ttl = expires - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            ttl = TimeSpan.FromMinutes(1);
        }

        // DistributedCacheAdapter only honours AbsoluteExpirationRelativeToNow / SlidingExpiration.
        await cache.SetAsync(
            key,
            data,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            });

        logger.LogDebug(
            "Renewed auth ticket {Key}. New expiry: {Expires}",
            key,
            expires);
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var data = await cache.GetAsync(key);
        if (data is null || data.Length == 0)
        {
            logger.LogWarning(
                "Auth ticket not found for key {Key}. User will need to re-authenticate.",
                key);
            return null;
        }

        var ticket = TicketSerializer.Default.Deserialize(data);

        logger.LogDebug(
            "Retrieved auth ticket {Key}. Expires: {Expires}",
            key,
            ticket?.Properties?.ExpiresUtc);

        return ticket;
    }

    public async Task RemoveAsync(string key)
    {
        logger.LogDebug("Removing auth ticket {Key}", key);
        await cache.RemoveAsync(key);
    }
}
