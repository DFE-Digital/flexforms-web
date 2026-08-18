using GovUK.Dfe.CoreLibs.Caching.Helpers;
using GovUK.Dfe.CoreLibs.Caching.Interfaces;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using System.ComponentModel;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Web.Services
{
    public class ApplicationStatusService : IApplicationStatusService
    {
        private readonly ITemplatesClient _templatesClient;
        private readonly ILogger<ApplicationStatusService> _logger;
        private readonly ICacheService<IMemoryCacheType> _cacheService;

        public ApplicationStatusService(ITemplatesClient templatesClient, ILogger<ApplicationStatusService> logger, ICacheService<IMemoryCacheType> cacheService)
        {
            _templatesClient = templatesClient;
            _logger = logger;
            _cacheService = cacheService;
        }

        public async Task<IReadOnlyList<CustomApplicationStatusDto>> GetCustomApplicationStatusesAsync(Guid? templateId)
        {
            if (!templateId.HasValue)
            {
                return new List<CustomApplicationStatusDto>();
            }

            var cacheKey = $"CustomApplicationStatuses_{CacheKeyHelper.GenerateHashedCacheKey(templateId.ToString())}";
            var methodName = nameof(GetCustomApplicationStatusesAsync);
            try
            {
                return await _cacheService.GetOrAddAsync(
                    cacheKey,
                    () => _templatesClient.GetCustomApplicationStatusesAsync(templateId.Value),
                    methodName);
            }
            catch (ExternalApplicationsException ex) when (ex.StatusCode is 401 or 403)
            {
                _logger.LogWarning(
                    ex,
                    "Could not load custom application statuses for template {TemplateId}; using default labels",
                    templateId);
                return new List<CustomApplicationStatusDto>();
            }
        }

        public KeyValuePair<ApplicationStatus, string> GetCalculatedApplicationStatusAsync(ApplicationDto application, IReadOnlyList<CustomApplicationStatusDto> customStatuses)
        {

            try
            {
                // If already submitted, return submitted
                if (application.Status == ApplicationStatus.Submitted)
                {
                    return new KeyValuePair<ApplicationStatus, string>(ApplicationStatus.Submitted, GetStatusLabel(ApplicationStatus.Submitted, customStatuses));
                }

                // Check if there's any response data indicating progress
                if (application.LatestResponse?.ResponseBody != null)
                {
                    try
                    {
                        // Try to decode base64 first
                        string responseJson;
                        try
                        {
                            var decodedBytes = Convert.FromBase64String(application.LatestResponse.ResponseBody);
                            responseJson = System.Text.Encoding.UTF8.GetString(decodedBytes);
                        }
                        catch
                        {
                            // If base64 decode fails, treat as plain JSON
                            responseJson = application.LatestResponse.ResponseBody;
                        }

                        var responseData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseJson);
                        if (responseData != null && responseData.Any())
                        {
                            // Check if there's any actual field data (not just task status)
                            var hasFieldData = responseData.Any(kvp =>
                                !kvp.Key.StartsWith("TaskStatus_") &&
                                kvp.Value.ValueKind != JsonValueKind.Null &&
                                !string.IsNullOrWhiteSpace(kvp.Value.ToString()));

                            if (hasFieldData)
                            {
                                return new KeyValuePair<ApplicationStatus, string>(ApplicationStatus.InProgress, GetStatusLabel(ApplicationStatus.InProgress, customStatuses));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse response data for application {ApplicationId}", application.ApplicationId);
                    }
                }

                // No response data = InProgress (default state for new applications)
                var currentStatus = application.Status.HasValue ? application.Status.Value : ApplicationStatus.Created;
                return new KeyValuePair<ApplicationStatus, string>(currentStatus, GetStatusLabel(currentStatus, customStatuses));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate application status for {ApplicationId}, defaulting to InProgress",
                    application.ApplicationId);
                return new KeyValuePair<ApplicationStatus, string>(ApplicationStatus.InProgress, GetStatusLabel(ApplicationStatus.InProgress, customStatuses));
            }
        }

        public string GetStatusLabel(ApplicationStatus status, IReadOnlyList<CustomApplicationStatusDto> customStatuses)
        {
            if (customStatuses != null)
            {
                var customStatus = customStatuses.FirstOrDefault(x => x.ApplicationStatus == status);
                if (customStatus?.Label != null)
                {
                    return customStatus.Label;
                }
            }

            return GetBaseStatusLabel(status);
        }

        public List<KeyValuePair<ApplicationStatus, string>> GetBaseApplicationStatuses()
        {
            List<KeyValuePair<ApplicationStatus, string>> baseStatuses = new List<KeyValuePair<ApplicationStatus, string>>();
            foreach(var status in Enum.GetValues<ApplicationStatus>())
            {
                baseStatuses.Add(new KeyValuePair<ApplicationStatus, string>(status, GetBaseStatusLabel(status)));
            }
            return baseStatuses;
        }

        public string GetBaseStatusLabel(ApplicationStatus status)
        {
            var appStatusInfo = status.GetType().GetField(status.ToString());
            var descriptionAttributes = (DescriptionAttribute[])appStatusInfo!.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return descriptionAttributes.Length > 0 ? descriptionAttributes[0].Description : status.ToString();
        }

        public async Task OverrideApplicationStatusLabels(Guid templateId, CustomApplicationStatusRequest customStatus)
        {
            var statusDto = await _templatesClient.CreateCustomApplicationStatusAsync(templateId, customStatus);
        }
    }
}
