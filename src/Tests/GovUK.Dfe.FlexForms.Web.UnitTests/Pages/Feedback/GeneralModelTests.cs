using GovUK.Dfe.FlexForms.Web.Pages.Feedback;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Feedback;

public class GeneralModelTests : FeedbackModelTests<GeneralModel, FeedbackOrSuggestion>
{
    protected override FeedbackOrSuggestion ExpectedRequestForModel =>
        new(Model.Message, Model.ReferenceNumber, (SatisfactionScore)Model.SatisfactionScore!, Model.TemplateId);
    
    [Fact]
    public async Task OnPostAsync_when_SatisfactionScore_is_null_then_validation_messages_are_returned_to_user()
    {
        string[] expectedSatisfactionScoreModelErrors = ["You must choose an option"];
        Model.SatisfactionScore = null;
        
        var result = await Model.OnPostAsync();
        
        Assert.IsType<PageResult>(result);
        
        Assert.False(Model.ModelState.IsValid);
        var satisfactionScoreModelState = Assert.Contains("SatisfactionScore", Model.ModelState);
        
        Assert.NotEmpty(satisfactionScoreModelState!.Errors);
        Assert.Equal(expectedSatisfactionScoreModelErrors, satisfactionScoreModelState.Errors.Select(e => e.ErrorMessage));
    }
}