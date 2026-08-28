using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Api.Client.Settings;
using Microsoft.AspNetCore.Mvc;

namespace GovUK.Dfe.FlexForms.Web.Pages.Feedback;

public class SupportRequestModel(
    IApplicationsClient applicationsClient,
    IFeedbackService feedbackService,
    IApiClientSettingsProvider apiClientSettingsProvider,
    ILogger<FeedbackModel<SupportRequest>> logger)
    : FeedbackModel<SupportRequest>(feedbackService, apiClientSettingsProvider, logger)
{
    [BindProperty] public string EmailAddress { get; set; } = null!;

    public IReadOnlyList<string> ApplicationReferences { get; private set; } = [];

    protected override UserFeedbackType UserFeedbackType => UserFeedbackType.SupportRequest;

    protected override SupportRequest BuildUserFeedbackRequest() =>
        new(Message, ReferenceNumber!, EmailAddress, TemplateId);

    protected override async Task FetchFormDataAsync()
    {
        // Anonymous users cannot list applications; leave the radio options empty.
        if (User.Identity?.IsAuthenticated == true)
        {
            var result = await applicationsClient.GetMyApplicationsAsync(templateId: TemplateId);
            ApplicationReferences = result.Items.AsEnumerable().Select(a => a.ApplicationReference).ToList();
        }

        await base.FetchFormDataAsync();
    }

    protected override void ValidateConditionalProperties()
    {
        if (ReferenceNumber is null)
        {
            ModelState.AddModelError(nameof(ReferenceNumber), "You must choose an option");
        }
    }
}
