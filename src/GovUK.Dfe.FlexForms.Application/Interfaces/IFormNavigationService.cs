namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    /// <summary>
    /// Handles navigation logic and URL generation for the form engine
    /// </summary>
    public interface IFormNavigationService
    {
        /// <summary>
        /// Gets the URL for the next page in the form
        /// </summary>
        /// <param name="currentPageId">The current page ID</param>
        /// <param name="taskId">The current task ID</param>
        /// <param name="referenceNumber">The application reference number</param>
        /// <returns>The URL for the next page</returns>
        string GetNextPageUrl(string currentPageId, string taskId, string referenceNumber);
        
        /// <summary>
        /// Gets the URL for the previous page in the form
        /// </summary>
        /// <param name="currentPageId">The current page ID</param>
        /// <param name="taskId">The current task ID</param>
        /// <param name="referenceNumber">The application reference number</param>
        /// <returns>The URL for the previous page</returns>
        string GetPreviousPageUrl(string currentPageId, string taskId, string referenceNumber);
        
        /// <summary>
        /// Gets the URL for the task summary page
        /// </summary>
        /// <param name="taskId">The task ID</param>
        /// <param name="referenceNumber">The application reference number</param>
        /// <returns>The URL for the task summary</returns>
        string GetTaskSummaryUrl(string taskId, string referenceNumber);
        
        /// <summary>
        /// Gets the URL for the application preview page
        /// </summary>
        /// <param name="referenceNumber">The application reference number</param>
        /// <returns>The URL for the application preview</returns>
        string GetApplicationPreviewUrl(string referenceNumber);
        
        /// <summary>
        /// Gets the URL for the task list page
        /// </summary>
        /// <param name="referenceNumber">The application reference number</param>
        /// <returns>The URL for the task list</returns>
        string GetTaskListUrl(string referenceNumber);
        
        /// <summary>
        /// Determines if navigation to a specific page is allowed
        /// </summary>
        /// <param name="pageId">The target page ID</param>
        /// <param name="taskId">The task ID</param>
        /// <param name="referenceNumber">The application reference number</param>
        /// <returns>True if navigation is allowed</returns>
        bool CanNavigateToPage(string pageId, string taskId, string referenceNumber);
        
        /// <summary>
        /// Gets the back link URL for the current context
        /// </summary>
        /// <param name="currentPageId">The current page ID</param>
        /// <param name="taskId">The current task ID</param>
        /// <param name="referenceNumber">The application reference number</param>
        /// <returns>The back link URL</returns>
        string GetBackLinkUrl(string currentPageId, string taskId, string referenceNumber);

        /// <summary>
        /// Gets the next navigation target after saving a page, considering the returnToSummaryPage property
        /// </summary>
        /// <param name="currentPage">The current page that was just saved</param>
        /// <param name="currentTask">The current task</param>
        /// <param name="referenceNumber">The application reference number</param>
        /// <returns>The URL for the next navigation target</returns>
        string GetNextNavigationTargetAfterSave(Domain.Models.Page currentPage, Domain.Models.Task currentTask, string referenceNumber);

        // Sub-flow URLs
        string GetCollectionFlowSummaryUrl(string taskId, string referenceNumber);
        string GetStartSubFlowUrl(string taskId, string referenceNumber, string flowId, string instanceId);
        string GetSubFlowPageUrl(string taskId, string referenceNumber, string flowId, string instanceId, string pageId);
    }
}
