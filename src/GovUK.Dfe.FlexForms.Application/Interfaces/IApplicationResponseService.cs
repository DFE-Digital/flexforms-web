using GovUK.Dfe.FlexForms.Domain.Models;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

public interface IApplicationResponseService
{
    Task SaveApplicationResponseAsync(Guid applicationId, Dictionary<string, object> formData, CancellationToken cancellationToken = default);
    string TransformToResponseJson(Dictionary<string, object> formData, Dictionary<string, string> taskStatusData, FormTemplate? template = null);
    void AccumulateFormData(Dictionary<string, object> newData);
    Dictionary<string, object> GetAccumulatedFormData();
    void ClearAccumulatedFormData();
    Dictionary<string, string> GetTaskStatusFromSession(Guid applicationId);
    void SaveTaskStatusToSession(Guid applicationId, string taskId, string status);
    void StoreFormDataInSession(Dictionary<string, object> formData);
    void SetCurrentAccumulatedApplicationId(Guid applicationId);
}
