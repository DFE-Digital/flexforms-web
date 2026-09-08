using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

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
    public void OnActionExecuting_ShouldSkipInterception_ForGetRequests()
    {
        var context = ExecutingContext(
            method: "GET",
            query: "",
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
    public void OnActionExecuting_ShouldUseConfirmationReturnOverride()
    {
        var form = ConfirmationForm();
        form = new FormCollection(new Dictionary<string, StringValues>(form)
        {
            ["confirmation-return-RemoveCollectionItem"] = "/applications/REF-1/t1"
        });

        var context = ExecutingContext(method: "POST", query: "", form: form);

        _filter.OnActionExecuting(context);

        _confirmations.Received().CreateConfirmation(Arg.Is<ConfirmationRequest>(r =>
            r.ReturnUrl == "/applications/REF-1/t1"));
    }

    [Fact]
    public void OnActionExecuting_ShouldProceed_WhenCreateConfirmationThrows()
    {
        _confirmations.CreateConfirmation(Arg.Any<ConfirmationRequest>())
            .Throws(new InvalidOperationException("store unavailable"));

        var context = ExecutingContext(method: "POST", query: "", form: ConfirmationForm());

        _filter.OnActionExecuting(context);

        Assert.Null(context.Result);
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

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldSkipInterception_ForGetRequests()
    {
        var page = CreatePageModel();
        var executing = PageExecutingContext(page, "GET", form: ConfirmationForm());
        PageHandlerExecutedContext? executed = null;

        await _filter.OnPageHandlerExecutionAsync(executing, () =>
        {
            executed = CreateExecutedContext(executing, new RedirectToPageResult("/done"));
            return Task.FromResult(executed);
        });

        Assert.IsType<RedirectToPageResult>(executed!.Result);
        _confirmations.DidNotReceive().CreateConfirmation(Arg.Any<ConfirmationRequest>());
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldSkipInterception_WhenConfirmedQueryIsTrue()
    {
        var page = CreatePageModel();
        var executing = PageExecutingContext(page, "POST", query: "?confirmed=true", form: ConfirmationForm());
        PageHandlerExecutedContext? executed = null;

        await _filter.OnPageHandlerExecutionAsync(executing, () =>
        {
            executed = CreateExecutedContext(executing, new RedirectToPageResult("/done"));
            return Task.FromResult(executed);
        });

        Assert.IsType<RedirectToPageResult>(executed!.Result);
        _confirmations.DidNotReceive().CreateConfirmation(Arg.Any<ConfirmationRequest>());
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirectToConfirmation_AfterSuccessfulPost()
    {
        var page = CreatePageModel();
        var executing = PageExecutingContext(page, "POST", form: ConfirmationForm());
        PageHandlerExecutedContext? executed = null;

        await _filter.OnPageHandlerExecutionAsync(executing, () =>
        {
            executed = CreateExecutedContext(executing, new RedirectToPageResult("/done"));
            return Task.FromResult(executed);
        });

        var redirect = Assert.IsType<RedirectToPageResult>(executed!.Result);
        Assert.Equal("/Confirmation/Index", redirect.PageName);
        _confirmations.Received().CreateConfirmation(Arg.Is<ConfirmationRequest>(r =>
            r.OriginalHandler == "RemoveCollectionItem" && r.DisplayFields.Contains("itemTitle")));
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldUseConfirmationReturnOverride()
    {
        var form = ConfirmationForm();
        form = new FormCollection(new Dictionary<string, StringValues>(form)
        {
            ["confirmation-return-RemoveCollectionItem"] = "/applications/REF-1/back"
        });
        var page = CreatePageModel();
        var executing = PageExecutingContext(page, "POST", form: form);
        PageHandlerExecutedContext? executed = null;

        await _filter.OnPageHandlerExecutionAsync(executing, () =>
        {
            executed = CreateExecutedContext(executing, new RedirectToPageResult("/done"));
            return Task.FromResult(executed);
        });

        _confirmations.Received().CreateConfirmation(Arg.Is<ConfirmationRequest>(r =>
            r.ReturnUrl == "/applications/REF-1/back"));
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldNotIntercept_WhenModelStateIsInvalid()
    {
        var page = CreatePageModel();
        page.ModelState.AddModelError("itemTitle", "Required");
        var executing = PageExecutingContext(page, "POST", form: ConfirmationForm());
        PageHandlerExecutedContext? executed = null;

        await _filter.OnPageHandlerExecutionAsync(executing, () =>
        {
            executed = CreateExecutedContext(executing, new PageResult());
            return Task.FromResult(executed);
        });

        Assert.IsType<PageResult>(executed!.Result);
        _confirmations.DidNotReceive().CreateConfirmation(Arg.Any<ConfirmationRequest>());
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldProceed_WhenCreateConfirmationThrows()
    {
        _confirmations.CreateConfirmation(Arg.Any<ConfirmationRequest>())
            .Throws(new InvalidOperationException("store unavailable"));

        var page = CreatePageModel();
        var executing = PageExecutingContext(page, "POST", form: ConfirmationForm());
        var originalResult = new RedirectToPageResult("/done");
        PageHandlerExecutedContext? executed = null;

        await _filter.OnPageHandlerExecutionAsync(executing, () =>
        {
            executed = CreateExecutedContext(executing, originalResult);
            return Task.FromResult(executed);
        });

        Assert.Same(originalResult, executed!.Result);
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

    private sealed class TestPageModel : PageModel;

    private static TestPageModel CreatePageModel()
    {
        var httpContext = new DefaultHttpContext();
        var pageContext = CreatePageContext(httpContext);
        return new TestPageModel { PageContext = pageContext };
    }

    private static PageContext CreatePageContext(HttpContext httpContext, ModelStateDictionary? modelState = null)
    {
        modelState ??= new ModelStateDictionary();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new CompiledPageActionDescriptor(),
            modelState);

        return new PageContext(actionContext)
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState)
        };
    }

    private static PageHandlerExecutingContext PageExecutingContext(
        TestPageModel page,
        string method,
        string query = "",
        IFormCollection? form = null)
    {
        var httpContext = page.HttpContext;
        httpContext.Request.Method = method;
        httpContext.Request.Path = "/applications/REF-1/t1";
        httpContext.Request.QueryString = new QueryString(query);
        if (form is not null)
        {
            httpContext.Request.ContentType = "application/x-www-form-urlencoded";
            httpContext.Features.Set<IFormFeature>(new FormFeature(form));
        }

        return new PageHandlerExecutingContext(
            CreatePageContext(httpContext, page.ModelState),
            Array.Empty<IFilterMetadata>(),
            handlerMethod: null,
            new Dictionary<string, object?>(),
            page);
    }

    private static PageHandlerExecutedContext CreateExecutedContext(
        PageHandlerExecutingContext executingContext,
        IActionResult result)
    {
        var page = (TestPageModel)executingContext.HandlerInstance;
        var executed = new PageHandlerExecutedContext(
            CreatePageContext(executingContext.HttpContext, page.ModelState),
            executingContext.Filters,
            executingContext.HandlerMethod,
            page)
        {
            Result = result
        };
        return executed;
    }
}
