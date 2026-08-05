using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Interfaces;

namespace GovUK.Dfe.FlexForms.Web.Services
{
    public class ImportedApplicationService(IApplicationsClient client) : IImportedApplicationService
    {
        public Task<bool> SaveApplicationAsync(string reference, IDictionary<string, object> data)
        {
            // TODO call the API client to create the application, add the response, and submit the application
            return Task.FromResult(false);
        }
    }
}
