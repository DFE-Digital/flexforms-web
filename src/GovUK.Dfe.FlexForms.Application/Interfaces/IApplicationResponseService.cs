using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.AspNetCore.Http;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

public interface IApplicationResponseService
{
    Task SaveApplicationResponseAsync(Guid applicationId, Dictionary<string, object> formData, ISession session, CancellationToken cancellationToken = default);
    string TransformToResponseJson(Dictionary<string, object> formData, Dictionary<string, string> taskStatusData);
    void AccumulateFormData(Dictionary<string, object> newData, ISession session);
    Dictionary<string, object> GetAccumulatedFormData(ISession session);
    void ClearAccumulatedFormData(ISession session);
    Dictionary<string, string> GetTaskStatusFromSession(Guid applicationId, ISession session);
    void SaveTaskStatusToSession(Guid applicationId, string taskId, string status, ISession session);
    void StoreFormDataInSession(Dictionary<string, object> formData, ISession session);
    void SetCurrentAccumulatedApplicationId(Guid applicationId, ISession session);
} 