using GovUK.Dfe.FlexForms.Web.Services;

namespace GovUK.Dfe.FlexForms.Web.Interfaces
{
    public interface IApplicationImporter
    {
        Task<ApplicationImportResult> ImportSpreadsheet(Guid templateId, Stream stream, IDictionary<string, string> mapping);
    }
}
