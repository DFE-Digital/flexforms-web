using AutoFixture;
using GovUK.Dfe.FlexForms.Web.Pages.Feedback;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Feedback;

public class SupportRequestModelTests : FeedbackModelTests<SupportRequestModel, SupportRequest>
{
    private readonly IApplicationsClient _applicationsClient;
    
    public SupportRequestModelTests()
    {
        _applicationsClient = Fixture.Create<IApplicationsClient>();
        Fixture.Inject(_applicationsClient);
    }

    protected override SupportRequest ExpectedRequestForModel =>
        new(Model.Message, Model.ReferenceNumber!, Model.EmailAddress, Model.TemplateId);

    [Fact]
    public async Task OnGetAsync_fetches_reference_numbers_for_radio_buttons()
    {
        await Model.OnGetAsync();

        await _applicationsClient.Received().GetMyApplicationsAsync(templateId: Model.TemplateId);
    }

    [Fact]
    public async Task OnPostAsync_fetches_reference_numbers_for_radio_buttons()
    {
        await Model.OnPostAsync();
        
        await _applicationsClient.Received().GetMyApplicationsAsync(templateId: Model.TemplateId);
    }
    
    [Fact]
    public async Task OnPostAsync_when_ReferenceNumber_is_null_then_validation_messages_are_returned_to_user()
    {
        string[] expectedReferenceNumberModelErrors = ["You must choose an option"];
        Model.ReferenceNumber = null;
        
        var result = await Model.OnPostAsync();
        
        Assert.IsType<PageResult>(result);
        
        Assert.False(Model.ModelState.IsValid);
        var referenceNumberModelState = Assert.Contains("ReferenceNumber", Model.ModelState);
        
        Assert.NotEmpty(referenceNumberModelState!.Errors);
        Assert.Equal(expectedReferenceNumberModelErrors, referenceNumberModelState.Errors.Select(e => e.ErrorMessage));
    }
}