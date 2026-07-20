using System.Text;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Web.Pages.Feedback;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Feedback;

public abstract class FeedbackModelTests<T, TReq> where T : FeedbackModel<TReq> where TReq : UserFeedbackRequest
{
    protected readonly IFixture Fixture;

    protected readonly IFeedbackService FeedbackService;
    protected readonly ApiClientSettings ApiClientSettings;
    protected readonly ILogger<T> Logger;
    protected readonly HttpContext HttpContext;

    private T? _model;

    protected T Model => _model ??= Fixture.Create<T>();
    
    protected abstract TReq ExpectedRequestForModel { get; }
    
    protected FeedbackModelTests()
    {
        Fixture = new Fixture().Customize(new AutoNSubstituteCustomization { ConfigureMembers = true });
        
        FeedbackService = Fixture.Create<IFeedbackService>();
        Fixture.Inject(FeedbackService);
        
        ApiClientSettings = Fixture.Create<ApiClientSettings>();
        Fixture.Inject(ApiClientSettings);
        
        Logger = Fixture.Create<ILogger<T>>();
        Fixture.Inject(Logger);
        
        HttpContext = Fixture.Create<HttpContext>();
        Fixture.Register(() => new PageContext { HttpContext = HttpContext });
    }

    [Fact]
    public async Task OnPostAsync_when_valid_model_then_submits_feedback()
    {
        var result = await Model.OnPostAsync();

        await FeedbackService.Received().SubmitFeedbackAsync(ExpectedRequestForModel);
        
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Feedback/ThankYou", redirect.PageName);
    }

    [Fact]
    public async Task OnPostAsync_when_feedback_service_returns_validation_error_then_validation_messages_are_returned_to_user()
    {
        string[] expectedMessageModelErrors = ["Message is required"];
        var expectedModelErrors = new Dictionary<string, string[]> { ["Message"] = expectedMessageModelErrors };

        FeedbackService.SubmitFeedbackAsync(Arg.Any<TReq>())
            .Returns(new SubmitFeedbackResult.ValidationError(expectedModelErrors));

        var result = await Model.OnPostAsync();
        
        Assert.IsType<PageResult>(result);

        Assert.False(Model.ModelState.IsValid);
        var messageModelState = Assert.Contains("Message", Model.ModelState);
        
        Assert.NotEmpty(messageModelState!.Errors);
        Assert.Equal(expectedMessageModelErrors, messageModelState.Errors.Select(e => e.ErrorMessage));
    }

    [Theory]
    [InlineData("Message")]
    [InlineData("ReferenceNumber")]
    public void IsPropertyValid_returns_true_when_model_state_has_no_entry_for_property(string propertyName)
    {
        Assert.True(Model.IsPropertyValid(propertyName));
    }
    
    [Theory]
    [InlineData("Message")]
    [InlineData("ReferenceNumber")]
    public void IsPropertyValid_returns_false_when_model_state_has_entry_for_property(string propertyName)
    {
        Model.ModelState.AddModelError(propertyName, Fixture.Create<string>());
        
        Assert.False(Model.IsPropertyValid(propertyName));
    }

    [Fact]
    public void TemplateId_gets_from_http_session_when_available()
    {
        var expectedTemplateId = Fixture.Create<Guid>();
        HttpContext.Session.TryGetValue("TemplateId", out _).Returns(call =>
        {
            call[1] = Encoding.UTF8.GetBytes(expectedTemplateId.ToString());
            return true;
        });
        
        Assert.Equal(expectedTemplateId, Model.TemplateId);
    }
    
    [Fact]
    public void TemplateId_is_read_from_default_in_client_settings_when_not_in_session()
    {
        HttpContext.Session.Clear();
        
        Assert.Equal(ApiClientSettings.DefaultTemplateId, Model.TemplateId);
    }
    
    [Fact]
    public void TemplateId_throws_InvalidOperationException_when_not_in_session_and_no_default_in_client_settings()
    {
        HttpContext.Session.Clear();
        ApiClientSettings.DefaultTemplateId = null;
        
        Assert.Throws<InvalidOperationException>(() => Model.TemplateId);
    }
}