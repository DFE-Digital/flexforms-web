using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Web.Services
{
    public class ApplicationImportResult
    {
        public bool Success { get; set; }
        public IEnumerable<string>? Errors { get; set; }
        public FormTemplate? Template { get; set; }
        public IDictionary<string, object>? Data { get; set; }
    }
}
