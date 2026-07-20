using System.Text.Json;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    /// <summary>
    /// Session-backed implementation of INavigationHistoryService.
    /// Stores a capped stack per scope key.
    /// </summary>
    public class NavigationHistoryService(ILogger<NavigationHistoryService> logger) : INavigationHistoryService
    {
        private const string SessionPrefix = "NavHistory_";
        private const int MaxDepth = 25;

        public void Push(string scopeKey, string url, ISession session)
        {
            if (string.IsNullOrWhiteSpace(scopeKey) || string.IsNullOrWhiteSpace(url)) return;
            var key = SessionPrefix + scopeKey;
            var stack = Load(session, key);

            // Avoid pushing duplicates of the latest entry
            if (stack.Count == 0 || !string.Equals(stack[^1], url, StringComparison.OrdinalIgnoreCase))
            {
                stack.Add(url);
                if (stack.Count > MaxDepth)
                {
                    // Trim oldest
                    stack.RemoveAt(0);
                }
                Save(session, key, stack);
            }
        }

        public string? Peek(string scopeKey, ISession session)
        {
            if (string.IsNullOrWhiteSpace(scopeKey)) return null;
            var key = SessionPrefix + scopeKey;
            var stack = Load(session, key);
            return stack.Count > 0 ? stack[^1] : null;
        }

        public string? Pop(string scopeKey, ISession session)
        {
            if (string.IsNullOrWhiteSpace(scopeKey)) return null;
            var key = SessionPrefix + scopeKey;
            var stack = Load(session, key);
            if (stack.Count == 0) return null;
            var last = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            Save(session, key, stack);
            return last;
        }

        public void Clear(string scopeKey, ISession session)
        {
            if (string.IsNullOrWhiteSpace(scopeKey)) return;
            var key = SessionPrefix + scopeKey;
            try
            {
                session.Remove(key);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to clear navigation history for scope {ScopeKey}", scopeKey);
            }
        }

        private static List<string> Load(ISession session, string key)
        {
            try
            {
                var bytes = session.Get(key);
                if (bytes == null) return new List<string>();
                var json = System.Text.Encoding.UTF8.GetString(bytes);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                return list ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static void Save(ISession session, string key, List<string> values)
        {
            try
            {
                var json = JsonSerializer.Serialize(values);
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                session.Set(key, bytes);
            }
            catch
            {
                // swallow
            }
        }
    }
}


