using System.Text.Json;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    /// <summary>
    /// Session-backed implementation of INavigationHistoryService.
    /// Stores a capped stack per scope key.
    /// </summary>
    public class NavigationHistoryService(
        IFormSessionStore sessionStore,
        ILogger<NavigationHistoryService> logger) : INavigationHistoryService
    {
        private const string SessionPrefix = FormSessionKeys.NavHistoryPrefix;
        private const int MaxDepth = 25;

        public void Push(string scopeKey, string url)
        {
            if (string.IsNullOrWhiteSpace(scopeKey) || string.IsNullOrWhiteSpace(url)) return;
            var key = SessionPrefix + scopeKey;
            var stack = Load(key);

            // Avoid pushing duplicates of the latest entry
            if (stack.Count == 0 || !string.Equals(stack[^1], url, StringComparison.OrdinalIgnoreCase))
            {
                stack.Add(url);
                if (stack.Count > MaxDepth)
                {
                    stack.RemoveAt(0);
                }
                Save(key, stack);
            }
        }

        public string? Peek(string scopeKey)
        {
            if (string.IsNullOrWhiteSpace(scopeKey)) return null;
            var key = SessionPrefix + scopeKey;
            var stack = Load(key);
            return stack.Count > 0 ? stack[^1] : null;
        }

        public string? Pop(string scopeKey)
        {
            if (string.IsNullOrWhiteSpace(scopeKey)) return null;
            var key = SessionPrefix + scopeKey;
            var stack = Load(key);
            if (stack.Count == 0) return null;
            var last = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            Save(key, stack);
            return last;
        }

        public void Clear(string scopeKey)
        {
            if (string.IsNullOrWhiteSpace(scopeKey)) return;
            var key = SessionPrefix + scopeKey;
            try
            {
                sessionStore.Remove(key);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to clear navigation history for scope {ScopeKey}", scopeKey);
            }
        }

        private List<string> Load(string key)
        {
            try
            {
                var json = sessionStore.GetString(key);
                if (string.IsNullOrEmpty(json)) return new List<string>();
                var list = JsonSerializer.Deserialize<List<string>>(json);
                return list ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void Save(string key, List<string> values)
        {
            try
            {
                sessionStore.SetString(key, JsonSerializer.Serialize(values));
            }
            catch
            {
                // swallow
            }
        }
    }
}
