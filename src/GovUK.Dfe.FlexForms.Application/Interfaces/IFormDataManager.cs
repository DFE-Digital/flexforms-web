using Microsoft.AspNetCore.Http;

namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    /// <summary>
    /// Manages form data operations including loading, saving, and retrieving data
    /// </summary>
    public interface IFormDataManager
    {
        /// <summary>
        /// Gets the data for a specific page
        /// </summary>
        /// <param name="pageId">The page ID</param>
        /// <param name="applicationId">The application ID</param>
        /// <returns>The page data as a dictionary</returns>
        Task<Dictionary<string, object>> GetPageDataAsync(string pageId, string applicationId);
        
        /// <summary>
        /// Saves the data for a specific page
        /// </summary>
        /// <param name="pageId">The page ID</param>
        /// <param name="applicationId">The application ID</param>
        /// <param name="data">The data to save</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SavePageDataAsync(string pageId, string applicationId, Dictionary<string, object> data);
        
        /// <summary>
        /// Gets the data for a specific task
        /// </summary>
        /// <param name="taskId">The task ID</param>
        /// <param name="applicationId">The application ID</param>
        /// <returns>The task data as a dictionary</returns>
        Task<Dictionary<string, object>> GetTaskDataAsync(string taskId, string applicationId);
        
        /// <summary>
        /// Gets all data for an application
        /// </summary>
        /// <param name="applicationId">The application ID</param>
        /// <returns>The application data as a dictionary</returns>
        Task<Dictionary<string, object>> GetApplicationDataAsync(string applicationId);
        
        /// <summary>
        /// Accumulates form data in session storage
        /// </summary>
        /// <param name="data">The data to accumulate</param>
        /// <param name="session">The HTTP session</param>
        void AccumulateFormData(Dictionary<string, object> data, ISession session);
        
        /// <summary>
        /// Gets accumulated form data from session storage
        /// </summary>
        /// <param name="session">The HTTP session</param>
        /// <returns>The accumulated data as a dictionary</returns>
        Dictionary<string, object> GetAccumulatedFormData(ISession session);
        
        /// <summary>
        /// Clears accumulated form data from session storage
        /// </summary>
        /// <param name="session">The HTTP session</param>
        void ClearAccumulatedFormData(ISession session);
    }
}
