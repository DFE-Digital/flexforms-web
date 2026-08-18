using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    /// <summary>
    /// Service for managing application state, status, and session data
    /// </summary>
    public interface IApplicationStateService
    {
        /// <summary>
        /// Loads the application from the API on every call (no session cache for authorization).
        /// </summary>
        /// <exception cref="ApplicationAccessException">When the application does not exist or the user cannot access it.</exception>
        Task<(Guid? ApplicationId, ApplicationDto? Application)> EnsureApplicationIdAsync(string referenceNumber);

        /// <summary>
        /// Loads response data from API into session
        /// </summary>
        Task LoadResponseDataIntoSessionAsync(ApplicationDto application);

        /// <summary>
        /// Gets application status from session or default
        /// </summary>
        string GetApplicationStatus(Guid? applicationId);

        /// <summary>
        /// Checks if application is editable based on status
        /// </summary>
        bool IsApplicationEditable(string applicationStatus);

        /// <summary>
        /// Calculates task status based on form data and explicit status
        /// </summary>
        Domain.Models.TaskStatus CalculateTaskStatus(string taskId, FormTemplate template, Dictionary<string, object> formData, Guid? applicationId, string applicationStatus);

        /// <summary>
        /// Saves task status to session and API
        /// </summary>
        Task SaveTaskStatusAsync(Guid applicationId, string taskId, Domain.Models.TaskStatus status);

        /// <summary>
        /// Checks if all tasks in the template are completed
        /// </summary>
        bool AreAllTasksCompleted(FormTemplate template, Dictionary<string, object> formData, Guid? applicationId, string applicationStatus);

        /// <summary>
        /// Validates all required fields across all tasks for submission.
        /// Unlike AreAllTasksCompleted, this method checks actual field values rather than trusting explicit task status.
        /// This ensures that if a file was removed (e.g., by virus scanner), the validation fails.
        /// </summary>
        /// <param name="template">The form template</param>
        /// <param name="formData">The current form data</param>
        /// <param name="isFieldHidden">Optional predicate to check if a field is hidden by conditional logic</param>
        /// <returns>Dictionary of task IDs to their missing required field IDs</returns>
        Dictionary<string, List<string>> ValidateAllRequiredFieldsForSubmission(FormTemplate template, Dictionary<string, object> formData, Func<string, bool>? isFieldHidden = null);

        /// <summary>
        /// Converts JSON element to appropriate object type
        /// </summary>
        object GetJsonElementValue(System.Text.Json.JsonElement element);
    }
}
