using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Feedback;

public class ThankYouModel : PageModel
{
    [BindProperty(SupportsGet = true)] public UserFeedbackType UserFeedbackType { get; set; }
}