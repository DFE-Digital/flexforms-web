using GovUK.Dfe.FlexForms.Application.Exceptions;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    /// <summary>
    /// Implementation of application state service for managing application state, status, and session data
    /// </summary>
    public class ApplicationStateService(
        IApplicationsClient applicationsClient,
        IApplicationResponseService applicationResponseService,
        IFieldRequirementService fieldRequirementService,
        ILogger<ApplicationStateService> logger)
        : IApplicationStateService
    {
        public async Task<(Guid? ApplicationId, ApplicationDto? Application)> EnsureApplicationIdAsync(
            string referenceNumber,
            ISession session)
        {
            if (string.IsNullOrWhiteSpace(referenceNumber))
                throw new ApplicationAccessException(referenceNumber ?? string.Empty);

            ClearStaleSessionDataIfReferenceChanged(referenceNumber, session);

            ApplicationDto application;
            try
            {
                application = await applicationsClient.GetApplicationByReferenceAsync(referenceNumber);
            }
            catch (ExternalApplicationsException<ExceptionResponse> ex) when (ex.StatusCode is 404 or 403)
            {
                logger.LogError(
                    "Application {ReferenceNumber} not accessible (HTTP {StatusCode})",
                    referenceNumber,
                    ex.StatusCode);
                throw new ApplicationAccessException(referenceNumber);
            }
            catch (ExternalApplicationsException ex) when (ex.StatusCode is 404 or 403)
            {
                logger.LogError(
                    "Application {ReferenceNumber} not accessible (HTTP {StatusCode})",
                    referenceNumber,
                    ex.StatusCode);
                throw new ApplicationAccessException(referenceNumber);
            }

            if (application is null)
            {
                logger.LogWarning("Application {ReferenceNumber} was not returned by the API", referenceNumber);
                throw new ApplicationAccessException(referenceNumber);
            }

            PersistApplicationToSession(application, referenceNumber, session);
            await LoadResponseDataIntoSessionAsync(application, session);

            logger.LogDebug(
                "Loaded application {ApplicationId} from API for reference {ReferenceNumber}",
                application.ApplicationId,
                referenceNumber);

            return (application.ApplicationId, application);
        }

        public async Task LoadResponseDataIntoSessionAsync(ApplicationDto application, ISession session)
        {
            if (application.LatestResponse?.ResponseBody == null)
            {
                logger.LogInformation("No existing response data found for application {ApplicationReference}", application.ApplicationReference);
                applicationResponseService.ClearAccumulatedFormData(session);
                applicationResponseService.SetCurrentAccumulatedApplicationId(application.ApplicationId, session);
                return;
            }

            try
            {
                logger.LogInformation("Loading response data for application {ApplicationReference}.",
                    application.ApplicationReference);

                var responseJson = application.LatestResponse.ResponseBody;

                // Parse the response body JSON
                var responseData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseJson);

                if (responseData == null)
                {
                    logger.LogWarning("Failed to parse response JSON for application {ApplicationReference}", application.ApplicationReference);
                    return;
                }

                var formDataDict = new Dictionary<string, object>();

                foreach (var kvp in responseData)
                {
                    try
                    {
                        // Check if this is a task status field
                        if (kvp.Key.StartsWith("TaskStatus_"))
                        {
                            // Extract task status and restore to session
                            var taskId = kvp.Key.Substring("TaskStatus_".Length);
                            string statusValue = string.Empty;
                            
                            if (kvp.Value.ValueKind == JsonValueKind.Object && kvp.Value.TryGetProperty("value", out var statusElement))
                            {
                                statusValue = statusElement.GetString() ?? string.Empty;
                            }
                            else
                            {
                                statusValue = kvp.Value.GetString() ?? string.Empty;
                            }
                            
                            if (!string.IsNullOrEmpty(statusValue))
                            {
                                applicationResponseService.SaveTaskStatusToSession(application.ApplicationId, taskId, statusValue, session);
                                logger.LogDebug("Restored task status: {TaskId} = {Status}", taskId, statusValue);
                            }
                        }
                        else
                        {
                            // Handle form field data
                            if (kvp.Value.ValueKind == JsonValueKind.Object && kvp.Value.TryGetProperty("value", out var valueElement))
                            {
                                // Complex structure: {"field1": {"value": "actual_value", "completed": true}}
                                formDataDict[kvp.Key] = GetJsonElementValue(valueElement);
                            }
                            else
                            {
                                // Simple structure: {"field1": "actual_value"}
                                formDataDict[kvp.Key] = GetJsonElementValue(kvp.Value);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to process field {FieldName} for application {ApplicationReference}",
                            kvp.Key, application.ApplicationReference);
                    }
                }

                // Store in session using the same key structure as form submission
                applicationResponseService.StoreFormDataInSession(formDataDict, session);
                applicationResponseService.SetCurrentAccumulatedApplicationId(application.ApplicationId, session);

                logger.LogInformation("Successfully loaded {FieldCount} fields from API into session for application {ApplicationReference}",
                    formDataDict.Count, application.ApplicationReference);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load response data for application {ApplicationReference}: {ErrorMessage}",
                    application.ApplicationReference, ex.Message);
            }
        }

        private void ClearStaleSessionDataIfReferenceChanged(string referenceNumber, ISession session)
        {
            var sessionReference = session.GetString("ApplicationReference");
            if (string.IsNullOrEmpty(sessionReference)
                || string.Equals(sessionReference, referenceNumber, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            applicationResponseService.ClearAccumulatedFormData(session);
            session.Remove("ApplicationId");
            session.Remove("ApplicationReference");
        }

        private void PersistApplicationToSession(ApplicationDto application, string referenceNumber, ISession session)
        {
            session.SetString("ApplicationId", application.ApplicationId.ToString());
            session.SetString("ApplicationReference", application.ApplicationReference ?? referenceNumber);

            var templateSchemaKey = $"TemplateSchema_{referenceNumber}";
            var templateVersionIdKey = $"TemplateVersionId_{referenceNumber}";
            var templateVersionNoKey = $"TemplateVersionNo_{referenceNumber}";

            if (application.TemplateSchema?.JsonSchema != null)
            {
                session.SetString(templateSchemaKey, application.TemplateSchema.JsonSchema);
                session.SetString(templateVersionIdKey, application.TemplateVersionId.ToString());
                session.SetString(templateVersionNoKey, application.TemplateSchema.VersionNumber ?? string.Empty);
            }

            if (application.Status != null)
            {
                var statusKey = $"ApplicationStatus_{application.ApplicationId}";
                session.SetString(statusKey, application.Status.ToString());
            }

            if (application.CreatedBy != null)
            {
                session.SetString($"ApplicationLeadApplicantName_{application.ApplicationId}", application.CreatedBy.Name);
                session.SetString($"ApplicationLeadApplicantEmail_{application.ApplicationId}", application.CreatedBy.Email);
                session.SetString($"ApplicationLeadApplicantUserId_{application.ApplicationId}", application.CreatedBy.UserId.ToString());
            }

            session.SetString(
                $"ApplicationFormVersion_{application.ApplicationId}",
                string.IsNullOrEmpty(application.TemplateSchema?.VersionNumber)
                    ? "N/A"
                    : application.TemplateSchema.VersionNumber);
        }

        public string GetApplicationStatus(Guid? applicationId, ISession session)
        {
            if (applicationId.HasValue)
            {
                var statusKey = $"ApplicationStatus_{applicationId.Value}";
                return session.GetString(statusKey) ?? "InProgress";
            }
            return "InProgress";
        }

        public bool IsApplicationEditable(string applicationStatus)
        {
            return applicationStatus.Equals("InProgress", StringComparison.OrdinalIgnoreCase);
        }

        public Domain.Models.TaskStatus CalculateTaskStatus(string taskId, FormTemplate template, Dictionary<string, object> formData, Guid? applicationId, ISession session, string applicationStatus)
        {
            // If application is submitted, all tasks are completed
            if (applicationStatus.Equals("Submitted", StringComparison.OrdinalIgnoreCase))
            {
                return Domain.Models.TaskStatus.Completed;
            }
            
            // First check if task is explicitly marked as completed
            if (applicationId.HasValue)
            {
                var sessionKey = $"TaskStatus_{applicationId.Value}_{taskId}";
                var statusString = session.GetString(sessionKey);
                
                if (!string.IsNullOrEmpty(statusString) && 
                    Enum.TryParse<Domain.Models.TaskStatus>(statusString, out var explicitStatus) &&
                    explicitStatus == Domain.Models.TaskStatus.Completed)
                {
                    return Domain.Models.TaskStatus.Completed;
                }
            }
            
            // Find the task in the template
            var task = template?.TaskGroups?
                .SelectMany(g => g.Tasks)
                .FirstOrDefault(t => t.TaskId == taskId);
                
            if (task == null)
            {
                return Domain.Models.TaskStatus.NotStarted;
            }
            
            // Check if any fields in this task have been completed
            var taskFieldIds = new List<string>();
            
            // For regular tasks, get field IDs from pages
            if (task.Pages != null)
            {
                taskFieldIds.AddRange(task.Pages
                    .SelectMany(p => p.Fields)
                    .Select(f => f.FieldId));
            }
            
            // For multi-collection flow tasks, also check collection field IDs
            if (task.Summary?.Mode?.Equals("multiCollectionFlow", StringComparison.OrdinalIgnoreCase) == true &&
                task.Summary.Flows != null)
            {
                taskFieldIds.AddRange(task.Summary.Flows.Select(f => f.FieldId));
            }
            
            // For derived collection flow tasks, also check derived field IDs (including derived item-specific keys)
            if (task.Summary?.Mode?.Equals("derivedCollectionFlow", StringComparison.OrdinalIgnoreCase) == true &&
                task.Summary.DerivedFlows != null)
            {
                taskFieldIds.AddRange(task.Summary.DerivedFlows.Select(f => f.FieldId));
            }

            // Detect any evidence of data for this task
            var hasAnyFieldCompleted = taskFieldIds.Any(fieldId =>
            {
                // Exact field key present with non-empty value
                if (formData.TryGetValue(fieldId, out var directValue) && !string.IsNullOrWhiteSpace(directValue?.ToString()))
                {
                    return true;
                }

                // For derived flows, item keys are usually like: {fieldId}_status_{itemId} or {fieldId}_data_{itemId}
                // Treat presence of any such non-empty key as progress
                var prefix = fieldId + "_";
                var anyPrefixed = formData.Any(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(kvp.Value?.ToString()));
                return anyPrefixed;
            });
            
            if (hasAnyFieldCompleted)
            {
                return Domain.Models.TaskStatus.InProgress;
            }
            
            return Domain.Models.TaskStatus.NotStarted;
        }

        public async Task SaveTaskStatusAsync(Guid applicationId, string taskId, Domain.Models.TaskStatus status, ISession session)
        {
            // Save task status to session
            applicationResponseService.SaveTaskStatusToSession(applicationId, taskId, status.ToString(), session);
            
            // Save all accumulated data (including task status) to API
            var formData = new Dictionary<string, object>(); // Empty form data since we're just updating task status
            await applicationResponseService.SaveApplicationResponseAsync(applicationId, formData, session);
        }

        public bool AreAllTasksCompleted(FormTemplate template, Dictionary<string, object> formData, Guid? applicationId, ISession session, string applicationStatus)
        {
            if (template?.TaskGroups == null)
            {
                return false;
            }

            var allTasks = template.TaskGroups.SelectMany(g => g.Tasks).ToList();
            
            return allTasks.All(task => 
                CalculateTaskStatus(task.TaskId, template, formData, applicationId, session, applicationStatus) == Domain.Models.TaskStatus.Completed);
        }

        public object GetJsonElementValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    return element.GetDecimal();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                default:
                    return element.ToString();
            }
        }

        public Dictionary<string, List<string>> ValidateAllRequiredFieldsForSubmission(
            FormTemplate template, 
            Dictionary<string, object> formData, 
            Func<string, bool>? isFieldHidden = null)
        {
            var tasksWithMissingFields = new Dictionary<string, List<string>>();

            if (template?.TaskGroups == null)
            {
                return tasksWithMissingFields;
            }

            var allTasks = template.TaskGroups.SelectMany(g => g.Tasks).ToList();

            foreach (var task in allTasks)
            {
                // Get missing required fields for this task
                // This checks actual field values, not explicit task status
                var missingFields = fieldRequirementService.GetMissingRequiredFields(task, template, formData, isFieldHidden);

                if (missingFields.Any())
                {
                    tasksWithMissingFields[task.TaskId] = missingFields;
                    logger.LogWarning(
                        "Task {TaskId} ({TaskName}) has {Count} missing required field(s) for submission: {FieldIds}",
                        task.TaskId,
                        task.TaskName,
                        missingFields.Count,
                        string.Join(", ", missingFields));
                }
            }

            return tasksWithMissingFields;
        }
    }
}
