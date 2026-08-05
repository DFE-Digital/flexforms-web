namespace GovUK.Dfe.FlexForms.Web.Interfaces
{
    public interface IImportedApplicationService
    {
        Task<bool> SaveApplicationAsync(string reference, IDictionary<string, object> data);
    }
}
