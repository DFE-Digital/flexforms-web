using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Pages.Confirmation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Confirmation;

public class ConfirmationIndexModelTests
{
    private readonly IButtonConfirmationService _confirmations = Substitute.For<IButtonConfirmationService>();
    private readonly IndexModel _model;

    public ConfirmationIndexModelTests()
    {
        _model = new IndexModel(_confirmations, NullLogger<IndexModel>.Instance);
        var httpContext = new DefaultHttpContext();
        _model.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
        _model.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
    }

    [Fact]
    public void OnGet_ShouldRedirectToError_WhenTokenIsMissing()
    {
        var result = Assert.IsType<RedirectToPageResult>(_model.OnGet(""));
        Assert.Equal("/Error/General", result.PageName);
    }

    [Fact]
    public void OnPost_ShouldRedirectPreservingMethod_WhenUserConfirms()
    {
        const string token = "tok-1";
        _model.ConfirmationToken = token;
        _model.HttpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["Confirmed"] = "true"
        });
        _confirmations.GetConfirmation(token).Returns(new ConfirmationContext
        {
            Token = token,
            Request = new ConfirmationRequest
            {
                OriginalPagePath = "/applications/REF-1/t1",
                OriginalHandler = "RemoveCollectionItem",
                OriginalFormData = new Dictionary<string, object> { ["itemId"] = "i1" },
                ReturnUrl = "/applications/REF-1/t1"
            },
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });

        var result = Assert.IsType<RedirectResult>(_model.OnPost());

        Assert.Equal("/applications/REF-1/t1?confirmed=true&handler=RemoveCollectionItem", result.Url);
        Assert.True(result.PreserveMethod);
        Assert.Equal("{\"itemId\":\"i1\"}", _model.TempData["ConfirmedFormData"]);
        Assert.Equal("RemoveCollectionItem", _model.TempData["ConfirmedHandler"]);
        _confirmations.Received().ClearConfirmation(token);
    }

    [Fact]
    public void OnPost_ShouldReturnToOriginalUrl_WhenUserCancels()
    {
        const string token = "tok-1";
        _model.ConfirmationToken = token;
        _model.HttpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["Confirmed"] = "false"
        });
        _confirmations.GetConfirmation(token).Returns(new ConfirmationContext
        {
            Token = token,
            Request = new ConfirmationRequest { ReturnUrl = "/applications/REF-1/t1" },
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });

        var result = Assert.IsType<LocalRedirectResult>(_model.OnPost());

        Assert.Equal("/applications/REF-1/t1", result.Url);
        _confirmations.Received().ClearConfirmation(token);
    }
}
