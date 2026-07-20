using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Feedback;

public class IndexModel(ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "referenceNumber")]
    public string? ReferenceNumber { get; set; }

    [BindProperty] public UserFeedbackType? FeedbackType { get; set; } = null;

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        switch (FeedbackType)
        {
            case UserFeedbackType.BugReport:
                return RedirectToPage("/Feedback/BugReport", new { ReferenceNumber });
            case UserFeedbackType.SupportRequest:
                return RedirectToPage("/Feedback/SupportRequest", new { ReferenceNumber });
            case UserFeedbackType.FeedbackOrSuggestion:
                return RedirectToPage("/Feedback/General", new { ReferenceNumber });
            default:
                logger.LogError("Invalid user feedback type {FeedbackType}", FeedbackType);
                ModelState.AddModelError(nameof(FeedbackType), "Invalid user feedback type");
                return Page();
        }
    }
}