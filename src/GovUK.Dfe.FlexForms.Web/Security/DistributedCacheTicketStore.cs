using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Stores cookie authentication tickets server-side using an in-memory store.
/// This avoids issues with the distributed cache having short TTLs that evict tickets prematurely.
/// 
/// Note: In a multi-instance deployment, consider using Redis or another distributed cache
/// with proper TTL configuration for auth tickets.
/// </summary>
public sealed class DistributedCacheTicketStore(
    ILogger<DistributedCacheTicketStore> logger) : ITicketStore
{
    private const string KeyPrefix = "auth_ticket_";
    
    // Use a dedicated in-memory store for auth tickets to avoid cache eviction issues
    // This ensures tickets aren't evicted by the hybrid caching's short TTL
    private static readonly ConcurrentDictionary<string, (byte[] Data, DateTimeOffset Expires)> _ticketStore = new();

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = KeyPrefix + Guid.NewGuid().ToString("N");
        RenewAsync(key, ticket).GetAwaiter().GetResult();
        
        logger.LogDebug(
            "Stored new auth ticket with key {Key}. Expires: {Expires}",
            key,
            ticket.Properties?.ExpiresUtc);
        
        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var data = TicketSerializer.Default.Serialize(ticket);
        var expires = ticket.Properties?.ExpiresUtc ?? DateTimeOffset.UtcNow.AddHours(8);
        
        _ticketStore[key] = (data, expires);
        
        logger.LogDebug(
            "Renewed auth ticket {Key}. New expiry: {Expires}",
            key,
            expires);
        
        // Clean up expired tickets periodically
        CleanupExpiredTickets();
        
        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (!_ticketStore.TryGetValue(key, out var entry))
        {
            logger.LogWarning(
                "Auth ticket not found for key {Key}. User will need to re-authenticate.",
                key);
            return Task.FromResult<AuthenticationTicket?>(null);
        }
        
        // Check if expired
        if (entry.Expires < DateTimeOffset.UtcNow)
        {
            logger.LogDebug(
                "Auth ticket {Key} has expired (expired at {Expires}). Removing from store.",
                key,
                entry.Expires);
            _ticketStore.TryRemove(key, out _);
            return Task.FromResult<AuthenticationTicket?>(null);
        }
        
        var ticket = TicketSerializer.Default.Deserialize(entry.Data);
        
        logger.LogDebug(
            "Retrieved auth ticket {Key}. Expires: {Expires}",
            key,
            ticket?.Properties?.ExpiresUtc);
        
        return Task.FromResult(ticket);
    }

    public Task RemoveAsync(string key)
    {
        logger.LogDebug("Removing auth ticket {Key}", key);
        _ticketStore.TryRemove(key, out _);
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Periodically clean up expired tickets to prevent memory growth
    /// </summary>
    private void CleanupExpiredTickets()
    {
        var now = DateTimeOffset.UtcNow;
        var expiredKeys = _ticketStore
            .Where(kvp => kvp.Value.Expires < now)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in expiredKeys)
        {
            if (_ticketStore.TryRemove(key, out _))
            {
                logger.LogDebug("Cleaned up expired auth ticket {Key}", key);
            }
        }
        
        if (expiredKeys.Count > 0)
        {
            logger.LogDebug("Cleaned up {Count} expired auth tickets", expiredKeys.Count);
        }
    }
}


