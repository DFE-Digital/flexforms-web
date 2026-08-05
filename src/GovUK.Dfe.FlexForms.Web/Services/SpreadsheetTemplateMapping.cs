namespace GovUK.Dfe.FlexForms.Web.Services
{
    public class SpreadsheetTemplateMapping
    {
        public string? SheetName { get; set; }
        public IDictionary<string, string>? Maps { get; set; }
    }
}