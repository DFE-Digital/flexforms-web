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
        void Push(string scopeKey, string url);

        /// <summary>
        /// Returns, without removing, the most recent URL for the scope, or null if none.
        /// </summary>
        string? Peek(string scopeKey);

        /// <summary>
        /// Pops and returns the most recent URL for the scope, or null if none.
        /// </summary>
        string? Pop(string scopeKey);

        /// <summary>
        /// Clears the navigation history for the scope.
        /// </summary>
        void Clear(string scopeKey);
    }
}
