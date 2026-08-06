using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Interfaces;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Web.Services
{
    public class ImportedApplicationService(IApplicationsClient client) : IImportedApplicationService
    {
        private readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

        public async Task<bool> SaveApplicationAsync(string reference, IDictionary<string, object> data)
        {
            // TODO use logger instead of Debug.WriteLine

            ApplicationDto createdApplication = await client.CreateApplicationAsync(new CreateApplicationRequest    
            {
                TemplateId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                InitialResponseBody = "{}"
            });
            Debug.WriteLine($"Created application {createdApplication.ApplicationReference} with ID {createdApplication.ApplicationId}");

            Dictionary<string, object> fields = [];
            foreach (var kvp in data)
            {
                KeyValuePair<string, object> field = new(kvp.Key, new { kvp.Value, Completed = true });
                fields.Add(field.Key, field.Value);
            }
            string responseBody = JsonSerializer.Serialize(fields, jsonSerializerOptions);
            string encodedResponse = Convert.ToBase64String(Encoding.UTF8.GetBytes(responseBody));
            ApplicationResponseDto applicationResponse = await client.AddApplicationResponseAsync(createdApplication.ApplicationId, new AddApplicationResponseRequest
            {
                ResponseBody = encodedResponse
            });
            Debug.WriteLine($"Added application response {applicationResponse.ApplicationId} for application {createdApplication.ApplicationReference}");

            ApplicationDto submittedApplication = await client.SubmitApplicationAsync(createdApplication.ApplicationId);
            Debug.WriteLine($"Submitted application {submittedApplication.ApplicationReference} with status {submittedApplication.Status}");

            return true;
        }
    }
}
