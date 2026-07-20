using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Feedback;

public class GeneralModel(
    IFeedbackService feedbackService,
    ApiClientSettings apiClientSettings,
    ILogger<GeneralModel> logger) : FeedbackModel<FeedbackOrSuggestion>(feedbackService, apiClientSettings, logger)
{
    [BindProperty] public SatisfactionScore? SatisfactionScore { get; set; }

    protected override UserFeedbackType UserFeedbackType => UserFeedbackType.FeedbackOrSuggestion;

    protected override FeedbackOrSuggestion BuildUserFeedbackRequest() =>
        new(Message, ReferenceNumber, (SatisfactionScore)SatisfactionScore!, TemplateId);

    protected override void ValidateConditionalProperties()
    {
        if (SatisfactionScore is null)
        {
            ModelState.AddModelError(nameof(SatisfactionScore), "You must choose an option");
        }
    }
}