using Microsoft.AspNetCore.Http;

namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    /// <summary>
    /// Provides simple per-scope navigation history storage for computing back navigation.
    /// Scope typically includes reference number, task ID, and optionally flow/instance IDs.
    /// </summary>
    public interface INavigationHistoryService
    {
        /// <summary>
        /// Pushes a URL onto the navigation history stack for the given scope.
        /// </summary>
        /// <param name="scopeKey">A unique key identifying the navigation scope (e.g. reference:task[:flow:instance]).</param>
        /// <param name="url">The URL to push.</param>
        /// <param name="session">The HTTP session to store history in.</param>
        void Push(string scopeKey, string url, ISession session);

        /// <summary>
        /// Returns, without removing, the most recent URL for the scope, or null if none.
        /// </summary>
        /// <param name="scopeKey">A unique key identifying the navigation scope.</param>
        /// <param name="session">The HTTP session to read from.</param>
        /// <returns>The last URL or null.</returns>
        string? Peek(string scopeKey, ISession session);

        /// <summary>
        /// Pops and returns the most recent URL for the scope, or null if none.
        /// </summary>
        /// <param name="scopeKey">A unique key identifying the navigation scope.</param>
        /// <param name="session">The HTTP session to read/write.</param>
        /// <returns>The popped URL or null.</returns>
        string? Pop(string scopeKey, ISession session);

        /// <summary>
        /// Clears the navigation history for the scope.
        /// </summary>
        /// <param name="scopeKey">A unique key identifying the navigation scope.</param>
        /// <param name="session">The HTTP session to clear from.</param>
        void Clear(string scopeKey, ISession session);
    }
}


