namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    /// <summary>
    /// Manages the different states of the form engine and determines which view should be rendered
    /// </summary>
    public interface IFormStateManager
    {
        /// <summary>
        /// Gets the current form state based on the provided parameters
        /// </summary>
        /// <param name="referenceNumber">The application reference number</param>
        /// <param name="taskId">The current task ID (optional)</param>
        /// <param name="pageId">The current page ID (optional)</param>
        /// <returns>The current form state</returns>
        FormState GetCurrentState(string referenceNumber, string taskId, string pageId);
        
        /// <summary>
        /// Determines if the task list should be shown
        /// </summary>
        /// <param name="pageId">The current page ID</param>
        /// <returns>True if task list should be shown</returns>
        bool ShouldShowTaskList(string pageId);
        
        /// <summary>
        /// Determines if the task summary should be shown
        /// </summary>
        /// <param name="taskId">The current task ID</param>
        /// <param name="pageId">The current page ID</param>
        /// <returns>True if task summary should be shown</returns>
        bool ShouldShowTaskSummary(string taskId, string pageId);
        
        /// <summary>
        /// Determines if the application preview should be shown
        /// </summary>
        /// <param name="pageId">The current page ID</param>
        /// <returns>True if application preview should be shown</returns>
        bool ShouldShowApplicationPreview(string pageId);

        // Sub-flow support
        bool ShouldShowCollectionFlowSummary(Domain.Models.Task task);
        bool ShouldShowDerivedCollectionFlowSummary(Domain.Models.Task task);
        bool IsInSubFlow(string flowId, string pageId);
    }

    /// <summary>
    /// Represents the different states of the form engine
    /// </summary>
    public enum FormState
    {
        /// <summary>
        /// Shows the task list overview
        /// </summary>
        TaskList,
        
        /// <summary>
        /// Shows an individual form page
        /// </summary>
        FormPage,
        
        /// <summary>
        /// Shows the task summary
        /// </summary>
        TaskSummary,
        
        /// <summary>
        /// Shows the application preview
        /// </summary>
        ApplicationPreview,

        /// <summary>
        /// Shows a collection-flow based custom summary
        /// </summary>
        CollectionFlowSummary,

        /// <summary>
        /// Shows a derived collection-flow based summary
        /// </summary>
        DerivedCollectionFlowSummary,

        /// <summary>
        /// Shows a page within a sub-flow (linear mini-form)
        /// </summary>
        SubFlowPage
    }
}
