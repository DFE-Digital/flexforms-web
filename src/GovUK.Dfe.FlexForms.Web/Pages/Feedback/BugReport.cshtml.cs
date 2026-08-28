using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Settings;
using Microsoft.AspNetCore.Mvc;

namespace GovUK.Dfe.FlexForms.Web.Pages.Feedback;

public class BugReportModel(
    IFeedbackService feedbackService,
    IApiClientSettingsProvider apiClientSettingsProvider,
    ILogger<BugReportModel> logger) : FeedbackModel<BugReport>(feedbackService, apiClientSettingsProvider, logger)
{
    [BindProperty] public bool? AllowContact { get; set; } = null;
    [BindProperty] public string? EmailAddress { get; set; }

    protected override UserFeedbackType UserFeedbackType => UserFeedbackType.BugReport;

    protected override BugReport BuildUserFeedbackRequest() =>
        new(Message, ReferenceNumber, EmailAddress, TemplateId);

    protected override void ValidateConditionalProperties()
    {
        if (AllowContact is null)
        {
            ModelState.AddModelError(nameof(AllowContact), "You must choose an option");
        }
        
        if (AllowContact == true && string.IsNullOrWhiteSpace(EmailAddress))
        {
            ModelState.AddModelError(nameof(EmailAddress), "You must enter an email address");
        }
    }
}
