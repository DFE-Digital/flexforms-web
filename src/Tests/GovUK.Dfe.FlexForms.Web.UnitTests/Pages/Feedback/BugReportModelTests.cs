using GovUK.Dfe.FlexForms.Web.Pages.Feedback;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Feedback;

public class BugReportModelTests : FeedbackModelTests<BugReportModel, BugReport>
{
    protected override BugReport ExpectedRequestForModel =>
        new(Model.Message, Model.ReferenceNumber, Model.EmailAddress, Model.TemplateId);
    
    [Fact]
    public async Task OnPostAsync_when_AllowContact_is_null_then_validation_messages_are_returned_to_user()
    {
        string[] expectedAllowContactModelErrors = ["You must choose an option"];
        Model.AllowContact = null;
        
        var result = await Model.OnPostAsync();
        
        Assert.IsType<PageResult>(result);
        
        Assert.False(Model.ModelState.IsValid);
        var allowContactModelState = Assert.Contains("AllowContact", Model.ModelState);
        
        Assert.NotEmpty(allowContactModelState!.Errors);
        Assert.Equal(expectedAllowContactModelErrors, allowContactModelState.Errors.Select(e => e.ErrorMessage));
    }
    
    [Fact]
    public async Task OnPostAsync_when_AllowContact_is_true_and_EmailAddress_is_null_then_validation_messages_are_returned_to_user()
    {
        string[] expectedEmailAddressModelErrors = ["You must enter an email address"];
        Model.AllowContact = true;
        Model.EmailAddress = null;
        
        var result = await Model.OnPostAsync();
        
        Assert.IsType<PageResult>(result);
        
        Assert.False(Model.ModelState.IsValid);
        var emailAddressModelState = Assert.Contains("EmailAddress", Model.ModelState);
        
        Assert.NotEmpty(emailAddressModelState!.Errors);
        Assert.Equal(expectedEmailAddressModelErrors, emailAddressModelState.Errors.Select(e => e.ErrorMessage));
    }
}