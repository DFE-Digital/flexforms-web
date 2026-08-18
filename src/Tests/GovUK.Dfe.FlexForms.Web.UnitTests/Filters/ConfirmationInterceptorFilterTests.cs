using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Filters;

public class ConfirmationInterceptorFilterTests
{
    private readonly IButtonConfirmationService _confirmations = Substitute.For<IButtonConfirmationService>();
    private readonly ConfirmationInterceptorFilter _filter;

    public ConfirmationInterceptorFilterTests()
    {
        _confirmations.CreateConfirmation(Arg.Any<ConfirmationRequest>()).Returns("token-1");
        _filter = new ConfirmationInterceptorFilter(_confirmations, NullLogger<ConfirmationInterceptorFilter>.Instance);
    }

    [Fact]
    public void OnActionExecuting_ShouldSkipInterception_WhenConfirmedQueryIsTrue()
    {
        var context = ExecutingContext(
            method: "POST",
            query: "?confirmed=true",
            form: ConfirmationForm());

        _filter.OnActionExecuting(context);

        Assert.Null(context.Result);
        _confirmations.DidNotReceive().CreateConfirmation(Arg.Any<ConfirmationRequest>());
    }

    [Fact]
    public void OnActionExecuting_ShouldRedirectToConfirmation_WhenButtonRequiresConfirmation()
    {
        var context = ExecutingContext(
            method: "POST",
            query: "",
            form: ConfirmationForm());

        _filter.OnActionExecuting(context);

        var redirect = Assert.IsType<RedirectToPageResult>(context.Result);
        Assert.Equal("/Confirmation/Index", redirect.PageName);
        _confirmations.Received().CreateConfirmation(Arg.Is<ConfirmationRequest>(r => r.OriginalHandler == "RemoveCollectionItem"));
    }

    [Fact]
    public void OnActionExecuting_ShouldNotIntercept_WhenNoConfirmationButtonIsPresent()
    {
        var context = ExecutingContext(
            method: "POST",
            query: "",
            form: new FormCollection(new Dictionary<string, StringValues> { ["handler"] = "Page" }));

        _filter.OnActionExecuting(context);

        Assert.Null(context.Result);
        _confirmations.DidNotReceive().CreateConfirmation(Arg.Any<ConfirmationRequest>());
    }

    private static ActionExecutingContext ExecutingContext(string method, string query, IFormCollection form)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Path = "/applications/REF-1/t1";
        httpContext.Request.QueryString = new QueryString(query);
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Features.Set<IFormFeature>(new FormFeature(form));

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
    }

    private static FormCollection ConfirmationForm() =>
        new(new Dictionary<string, StringValues>
        {
            ["handler"] = "RemoveCollectionItem",
            ["confirmation-check-RemoveCollectionItem"] = "true",
            ["confirmation-display-fields-RemoveCollectionItem"] = "itemTitle",
            ["itemTitle"] = "Ada"
        });
}
