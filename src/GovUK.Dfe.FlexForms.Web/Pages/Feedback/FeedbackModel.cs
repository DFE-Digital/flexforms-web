using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Feedback;

public abstract class FeedbackModel<T>(IFeedbackService feedbackService, ApiClientSettings apiClientSettings, ILogger<FeedbackModel<T>> logger) : PageModel where T : UserFeedbackRequest
{
    [BindProperty(SupportsGet = true, Name = "referenceNumber")]
    public string? ReferenceNumber { get; set; }
    
    [BindProperty]
    public string Message { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        await FetchFormDataAsync();
        
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await FetchFormDataAsync();
        
        ValidateConditionalProperties();
        
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await TrySubmitFeedbackAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        return RedirectToPage("/Feedback/ThankYou", new { UserFeedbackType });
    }

    protected abstract UserFeedbackType UserFeedbackType { get; }
    protected abstract T BuildUserFeedbackRequest();

    protected virtual Task FetchFormDataAsync()
    {
        return Task.CompletedTask;
    }
    
    protected virtual void ValidateConditionalProperties() { }

    public bool IsPropertyValid(string propertyName) {
        if (ModelState.TryGetValue(propertyName, out var modelState))
        {
            return modelState.Errors.Count == 0;
        }

        return true;
    }

    public Guid TemplateId
    {
        get
        {
            var sessionTemplateId = HttpContext.Session.GetString("TemplateId");

            if (sessionTemplateId is not null && Guid.TryParse(sessionTemplateId, out var templateId))
            {
                return templateId;
            }

            logger.LogWarning("Could not find template ID in session, falling back to configured settings.");

            return apiClientSettings.DefaultTemplateId ??
                   throw new InvalidOperationException("No default template ID configured.");
        }
    }

    private async Task TrySubmitFeedbackAsync()
    {
        var result = await feedbackService.SubmitFeedbackAsync(BuildUserFeedbackRequest());

        if (result is SubmitFeedbackResult.ValidationError validationError)
        {
            foreach (var (key, errors) in validationError.Errors)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError(key, error);
                }
            }
        }
    }
}