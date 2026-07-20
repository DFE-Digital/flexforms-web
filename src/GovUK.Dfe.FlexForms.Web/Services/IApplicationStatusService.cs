using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Web.Services
{
    public interface IApplicationStatusService
    {
        Task<IReadOnlyList<CustomApplicationStatusDto>> GetCustomApplicationStatusesAsync(Guid? templateId);
        KeyValuePair<ApplicationStatus, string> GetCalculatedApplicationStatusAsync(ApplicationDto application, IReadOnlyList<CustomApplicationStatusDto> customStatuses);
        string GetStatusLabel(ApplicationStatus status, IReadOnlyList<CustomApplicationStatusDto> customStatuses);
        List<KeyValuePair<ApplicationStatus, string>> GetBaseApplicationStatuses();
        string GetBaseStatusLabel(ApplicationStatus status);
        Task OverrideApplicationStatusLabels(Guid templateId, CustomApplicationStatusRequest customStatus);
    }
}
