using System.ComponentModel;
using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Dashboard;

/// <summary>
/// Calculates dashboard display status from an application and custom labels.
/// Behaviour matches the previous Web ApplicationStatusService helpers.
/// </summary>
public static class DashboardApplicationStatusCalculator
{
    public static KeyValuePair<ApplicationStatus, string> GetCalculatedStatus(
        ApplicationDto application,
        IReadOnlyList<CustomApplicationStatusDto> customStatuses,
        ILogger logger)
    {
        try
        {
            if (application.Status == ApplicationStatus.Submitted)
            {
                return new KeyValuePair<ApplicationStatus, string>(
                    ApplicationStatus.Submitted,
                    GetStatusLabel(ApplicationStatus.Submitted, customStatuses));
            }

            if (application.LatestResponse?.ResponseBody != null)
            {
                try
                {
                    string responseJson;
                    try
                    {
                        var decodedBytes = Convert.FromBase64String(application.LatestResponse.ResponseBody);
                        responseJson = System.Text.Encoding.UTF8.GetString(decodedBytes);
                    }
                    catch
                    {
                        responseJson = application.LatestResponse.ResponseBody;
                    }

                    var responseData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseJson);
                    if (responseData != null && responseData.Any())
                    {
                        var hasFieldData = responseData.Any(kvp =>
                            !kvp.Key.StartsWith("TaskStatus_") &&
                            kvp.Value.ValueKind != JsonValueKind.Null &&
                            !string.IsNullOrWhiteSpace(kvp.Value.ToString()));

                        if (hasFieldData)
                        {
                            return new KeyValuePair<ApplicationStatus, string>(
                                ApplicationStatus.InProgress,
                                GetStatusLabel(ApplicationStatus.InProgress, customStatuses));
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse response data for application {ApplicationId}", application.ApplicationId);
                }
            }

            var currentStatus = application.Status.HasValue ? application.Status.Value : ApplicationStatus.Created;
            return new KeyValuePair<ApplicationStatus, string>(currentStatus, GetStatusLabel(currentStatus, customStatuses));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to calculate application status for {ApplicationId}, defaulting to InProgress",
                application.ApplicationId);
            return new KeyValuePair<ApplicationStatus, string>(
                ApplicationStatus.InProgress,
                GetStatusLabel(ApplicationStatus.InProgress, customStatuses));
        }
    }

    public static string GetStatusLabel(
        ApplicationStatus status,
        IReadOnlyList<CustomApplicationStatusDto>? customStatuses)
    {
        if (customStatuses != null)
        {
            var customStatus = customStatuses.FirstOrDefault(x => x.ApplicationStatus == status);
            if (customStatus?.Label != null)
                return customStatus.Label;
        }

        return GetBaseStatusLabel(status);
    }

    public static string GetBaseStatusLabel(ApplicationStatus status)
    {
        var appStatusInfo = status.GetType().GetField(status.ToString());
        var descriptionAttributes = (DescriptionAttribute[])appStatusInfo!.GetCustomAttributes(typeof(DescriptionAttribute), false);
        return descriptionAttributes.Length > 0 ? descriptionAttributes[0].Description : status.ToString();
    }
}
