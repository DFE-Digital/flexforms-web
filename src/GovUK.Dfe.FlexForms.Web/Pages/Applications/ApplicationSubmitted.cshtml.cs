using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Applications
{
    [ExcludeFromCodeCoverage]
    public class ApplicationSubmittedModel : PageModel
    {
        [BindProperty(SupportsGet = true, Name = "referenceNumber")] 
        public string ReferenceNumber { get; set; }

        public void OnGet()
        {
            // Page loads with reference number from route
        }
    }
} 