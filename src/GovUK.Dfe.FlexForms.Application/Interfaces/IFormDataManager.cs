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
        Task<Dictionary<string, object>> GetPageDataAsync(string pageId, string applicationId);

        /// <summary>
        /// Saves the data for a specific page
        /// </summary>
        Task SavePageDataAsync(string pageId, string applicationId, Dictionary<string, object> data);

        /// <summary>
        /// Gets the data for a specific task
        /// </summary>
        Task<Dictionary<string, object>> GetTaskDataAsync(string taskId, string applicationId);

        /// <summary>
        /// Gets all data for an application
        /// </summary>
        Task<Dictionary<string, object>> GetApplicationDataAsync(string applicationId);

        /// <summary>
        /// Accumulates form data in session storage
        /// </summary>
        void AccumulateFormData(Dictionary<string, object> data);

        /// <summary>
        /// Gets accumulated form data from session storage
        /// </summary>
        Dictionary<string, object> GetAccumulatedFormData();

        /// <summary>
        /// Clears accumulated form data from session storage
        /// </summary>
        void ClearAccumulatedFormData();
    }
}
