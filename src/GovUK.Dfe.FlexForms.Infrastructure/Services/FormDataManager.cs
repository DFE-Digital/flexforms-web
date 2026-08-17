using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    /// <summary>
    /// Implementation of the form data manager that handles data operations
    /// </summary>
    public class FormDataManager : IFormDataManager
    {
        private readonly IApplicationResponseService _applicationResponseService;
        private readonly ILogger<FormDataManager> _logger;

        public FormDataManager(
            IApplicationResponseService applicationResponseService,
            ILogger<FormDataManager> logger)
        {
            _applicationResponseService = applicationResponseService;
            _logger = logger;
        }

        public async Task<Dictionary<string, object>> GetPageDataAsync(string pageId, string applicationId)
        {
            _logger.LogDebug("Getting page data for page {PageId} and application {ApplicationId}", pageId, applicationId);
            return new Dictionary<string, object>();
        }

        public async Task SavePageDataAsync(string pageId, string applicationId, Dictionary<string, object> data)
        {
            if (Guid.TryParse(applicationId, out var appId))
            {
                await _applicationResponseService.SaveApplicationResponseAsync(appId, data);
                _logger.LogInformation("Saved page data for page {PageId} and application {ApplicationId}", pageId, applicationId);
            }
            else
            {
                _logger.LogWarning("Invalid application ID format: {ApplicationId}", applicationId);
            }
        }

        public async Task<Dictionary<string, object>> GetTaskDataAsync(string taskId, string applicationId)
        {
            _logger.LogDebug("Getting task data for task {TaskId} and application {ApplicationId}", taskId, applicationId);
            return new Dictionary<string, object>();
        }

        public async Task<Dictionary<string, object>> GetApplicationDataAsync(string applicationId)
        {
            _logger.LogDebug("Getting application data for application {ApplicationId}", applicationId);
            return new Dictionary<string, object>();
        }

        public void AccumulateFormData(Dictionary<string, object> data)
        {
            _applicationResponseService.AccumulateFormData(data);
            _logger.LogDebug("Accumulated {Count} form data entries in session", data.Count);
        }

        public Dictionary<string, object> GetAccumulatedFormData()
        {
            var data = _applicationResponseService.GetAccumulatedFormData();
            _logger.LogDebug("Retrieved {Count} accumulated form data entries from session", data.Count);
            return data;
        }

        public void ClearAccumulatedFormData()
        {
            _applicationResponseService.ClearAccumulatedFormData();
            _logger.LogDebug("Cleared accumulated form data from session");
        }
    }
}
