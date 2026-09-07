using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Exceptions;
using GovUK.Dfe.FlexForms.Web.Constants;
using GovUK.Dfe.FlexForms.Web.Filters;
using GovUK.Dfe.FlexForms.Web.Interfaces;
using GovUK.Dfe.FlexForms.Web.Services;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Filters;

public class ExternalApiExceptionFilterTests
{
    private readonly ExternalApiPageExceptionFilter _pageFilter;
    private readonly ExternalApiMvcExceptionFilter _mvcFilter;

    public ExternalApiExceptionFilterTests()
    {
        _pageFilter = new ExternalApiPageExceptionFilter(NullLogger<ExternalApiPageExceptionFilter>.Instance);
        _mvcFilter = new ExternalApiMvcExceptionFilter(NullLogger<ExternalApiMvcExceptionFilter>.Instance);
    }

    #region ExternalApiPageExceptionFilter – selection

    [Fact]
    public async Task OnPageHandlerSelectionAsync_ShouldComplete()
    {
        var page = CreatePageModel();
        var context = new PageHandlerSelectedContext(page.PageContext, Array.Empty<IFilterMetadata>(), page);

        var task = _pageFilter.OnPageHandlerSelectionAsync(context);

        Assert.True(task.IsCompletedSuccessfully);
        await task;
    }

    #endregion

    #region ExternalApiPageExceptionFilter – ApplicationAccessException

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirectToNotFound_WhenApplicationAccessExceptionThrown()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page);
        var executedContext = CreateExecutedContext(executingContext, new ApplicationAccessException("REF-1"));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal("/Error/NotFound", redirect.PageName);
        Assert.True(executedContext.ExceptionHandled);
    }

    #endregion

    #region ExternalApiPageExceptionFilter – plain ExternalApplicationsException (MapUnhandledApiException)

    [Theory]
    [InlineData(404, "/Error/NotFound", null)]
    [InlineData(401, "/Error/Forbidden", null)]
    [InlineData(500, "/Error/ServerError", null)]
    [InlineData(418, "/Error/General", "Unexpected error")]
    public async Task OnPageHandlerExecutionAsync_ShouldMapPlainApiException_ByStatusCode(
        int statusCode,
        string expectedPage,
        string? message)
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page, method: "POST", path: "/other");
        var exception = CreatePlainApiException(statusCode, message ?? $"Error {statusCode}");
        var executedContext = CreateExecutedContext(executingContext, exception);

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal(expectedPage, redirect.PageName);
        Assert.True(executedContext.ExceptionHandled);
        if (expectedPage == "/Error/General" && message is not null)
            Assert.Contains(message, page.TempData["ErrorMessage"]?.ToString());
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirectPlain403GetApplicationToNotFound()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page, method: "GET", path: "/applications/REF-1/t1");
        var executedContext = CreateExecutedContext(executingContext, CreatePlainApiException(403, "Forbidden"));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal("/Error/NotFound", redirect.PageName);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirectPlain403AuthFailureToLogout()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page, method: "POST", path: "/admin/settings");
        var executedContext = CreateExecutedContext(
            executingContext,
            CreatePlainApiException(403, "Access token expired"));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal("/Logout", redirect.PageName);
        Assert.Equal("token_expired", redirect.RouteValues!["reason"]);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirectPlain403OtherToForbidden()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page, method: "POST", path: "/admin/settings");
        var executedContext = CreateExecutedContext(
            executingContext,
            CreatePlainApiException(403, "Insufficient permissions"));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal("/Error/Forbidden", redirect.PageName);
    }

    #endregion

    #region ExternalApiPageExceptionFilter – typed ExternalApplicationsException<ExceptionResponse>

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldReturnPageResultWithModelState_WhenValidationErrorsInContext()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page);
        var validationJson = JsonSerializer.SerializeToElement(new Dictionary<string, string[]>
        {
            ["email"] = ["Email is required"],
            ["name"] = ["Name is too long"]
        });
        var response = new ExceptionResponse
        {
            StatusCode = 422,
            Message = "Validation failed",
            ErrorId = "111111",
            Context = new Dictionary<string, object> { ["validationErrors"] = validationJson }
        };
        var executedContext = CreateExecutedContext(
            executingContext,
            CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        Assert.IsType<PageResult>(executedContext.Result);
        Assert.True(executedContext.ExceptionHandled);
        Assert.Equal(2, page.ModelState.ErrorCount);
        Assert.Contains(page.ModelState["email"]!.Errors, e => e.ErrorMessage == "Email is required");
        Assert.Contains(page.ModelState["name"]!.Errors, e => e.ErrorMessage == "Name is too long");
    }

    [Theory]
    [InlineData(400, "Bad request")]
    [InlineData(409, "Conflict")]
    public async Task OnPageHandlerExecutionAsync_ShouldReturnPageResultWithMessage_When400Or409WithoutFieldErrors(
        int statusCode,
        string message)
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page);
        var response = new ExceptionResponse { StatusCode = statusCode, Message = message, ErrorId = "222222" };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        Assert.IsType<PageResult>(executedContext.Result);
        Assert.True(executedContext.ExceptionHandled);
        Assert.Contains(page.ModelState["Error"]!.Errors, e => e.ErrorMessage == message);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirect429ToGeneral_WithTempData()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page);
        var response = new ExceptionResponse
        {
            StatusCode = 429,
            Message = "Too many requests",
            ErrorId = "333333"
        };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal("/Error/General", redirect.PageName);
        Assert.Equal("333333", page.TempData["ApiErrorId"]);
        Assert.Equal("Too many requests", page.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirectTyped404ToNotFound()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page);
        var response = new ExceptionResponse { StatusCode = 404, Message = "Not found", ErrorId = "444444" };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal("/Error/NotFound", redirect.PageName);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirectTyped401ToForbidden_WithApiErrorId()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page);
        var response = new ExceptionResponse { StatusCode = 401, Message = "Unauthorized", ErrorId = "555555" };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal("/Error/Forbidden", redirect.PageName);
        Assert.Equal("555555", page.TempData["ApiErrorId"]);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirectTyped403GetApplicationToNotFound()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page, method: "GET", path: "/applications/REF-1/t1");
        var response = new ExceptionResponse
        {
            StatusCode = 403,
            Message = "No access",
            ErrorId = "666666"
        };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal("/Error/NotFound", redirect.PageName);
        Assert.Equal("No access", page.TempData["AccessDeniedReason"]);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirectTyped403AuthFailureToLogout()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page, method: "POST", path: "/admin/settings");
        var response = new ExceptionResponse
        {
            StatusCode = 403,
            Message = "Bearer token expired",
            ErrorId = "777777"
        };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal("/Logout", redirect.PageName);
        Assert.Equal("token_expired", redirect.RouteValues!["reason"]);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirectTyped403OtherToForbidden()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page, method: "POST", path: "/admin/settings");
        var response = new ExceptionResponse
        {
            StatusCode = 403,
            Message = "Role required",
            ErrorId = "888888"
        };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal("/Error/Forbidden", redirect.PageName);
        Assert.Equal("Role required", page.TempData["AccessDeniedReason"]);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirectTyped500ToServerError()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page);
        var response = new ExceptionResponse { StatusCode = 500, Message = "Server error", ErrorId = "999999" };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal("/Error/ServerError", redirect.PageName);
        Assert.Equal("999999", page.TempData["ApiErrorId"]);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirectTypedOtherStatusToGeneral()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(page);
        var response = new ExceptionResponse
        {
            StatusCode = 418,
            Message = "I am a teapot",
            ErrorId = "101010"
        };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal("/Error/General", redirect.PageName);
        Assert.Equal("101010", page.TempData["ApiErrorId"]);
        Assert.Equal("I am a teapot", page.TempData["ErrorMessage"]);
    }

    #endregion

    #region ExternalApiPageExceptionFilter – upload / file operation detection

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldDetectUploadViaQueryHandler()
    {
        var page = CreatePageModel();
        var form = UploadForm(fieldId: "fileField1");
        var executingContext = CreateExecutingContext(
            page,
            method: "POST",
            path: "/applications/REF-1/t1",
            query: "?handler=UploadFile",
            form: form);

        PageHandlerExecutedContext? capturedExecuted = null;
        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () =>
        {
            capturedExecuted = CreateExecutedContext(executingContext);
            return Task.FromResult(capturedExecuted);
        });

        Assert.True(executingContext.HttpContext.Items.ContainsKey("UploadRequestInfo"));
        Assert.True(executingContext.HttpContext.Items.ContainsKey("FileOperationInfo"));
        var uploadInfo = Assert.IsType<ValueTuple<bool, string>>(executingContext.HttpContext.Items["UploadRequestInfo"]);
        Assert.True(uploadInfo.Item1);
        Assert.Equal("fileField1", uploadInfo.Item2);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldDetectUploadViaHandlerMethodName()
    {
        var page = CreatePageModel();
        var form = UploadForm(fieldId: "attachment");
        var handlerMethod = new HandlerMethodDescriptor { Name = "OnPostUploadFileAsync" };
        var executingContext = CreateExecutingContext(
            page,
            method: "POST",
            path: "/applications/REF-1/t1",
            form: form,
            handlerMethod: handlerMethod);

        await _pageFilter.OnPageHandlerExecutionAsync(
            executingContext,
            () => Task.FromResult(CreateExecutedContext(executingContext)));

        var fileOpInfo = Assert.IsType<ValueTuple<bool, string, string>>(
            executingContext.HttpContext.Items["FileOperationInfo"]);
        Assert.True(fileOpInfo.Item1);
        Assert.Equal("upload", fileOpInfo.Item2);
        Assert.Equal("attachment", fileOpInfo.Item3);
    }

    #endregion

    #region ExternalApiPageExceptionFilter – file validation / access handlers

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldHandleFileValidationError_OnUpload400()
    {
        var formErrorStore = Substitute.For<IFormErrorStore>();
        var services = new ServiceCollection();
        services.AddSingleton(formErrorStore);
        var page = CreatePageModel(services.BuildServiceProvider());
        var form = UploadForm(fieldId: "uploadField", returnUrl: "/applications/REF-1/t1?pageId=p1");
        var executingContext = CreateExecutingContext(
            page,
            method: "POST",
            path: "/applications/REF-1/t1",
            form: form,
            handlerMethod: new HandlerMethodDescriptor { Name = "OnPostUploadFileAsync" });
        var response = new ExceptionResponse
        {
            StatusCode = 400,
            Message = "File type not allowed",
            ErrorId = "121212"
        };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectResult>(executedContext.Result);
        Assert.Equal("/applications/REF-1/t1?pageId=p1", redirect.Url);
        formErrorStore.Received().Save("uploadField", page.ModelState, null);
        Assert.Contains(page.ModelState["Error"]!.Errors, e => e.ErrorMessage == "File type not allowed");
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldReturnPageResult_WhenFileValidationErrorWithoutReturnUrl()
    {
        var page = CreatePageModel();
        var form = UploadForm(fieldId: "uploadField");
        var executingContext = CreateExecutingContext(
            page,
            method: "POST",
            path: "/applications/REF-1/t1",
            form: form,
            handlerMethod: new HandlerMethodDescriptor { Name = "OnPostUploadFileAsync" });
        var response = new ExceptionResponse
        {
            StatusCode = 422,
            Message = "Virus detected",
            ErrorId = "131313"
        };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        Assert.IsType<PageResult>(executedContext.Result);
        Assert.Contains(page.ModelState["Error"]!.Errors, e => e.ErrorMessage == "Virus detected");
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldHandleApplicationWriteAccessDenied_OnMutating403()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(
            page,
            method: "POST",
            path: "/applications/REF-1/t1",
            form: new FormCollection(new Dictionary<string, StringValues> { ["handler"] = "Page" }));
        var response = new ExceptionResponse
        {
            StatusCode = 403,
            Message = "Read only",
            ErrorId = "141414"
        };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        Assert.IsType<PageResult>(executedContext.Result);
        Assert.Contains(
            page.ModelState["Error"]!.Errors,
            e => e.ErrorMessage == ApplicationAccessMessages.NoWritePermission);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldHandleApplicationFileAccessDenied_OnUpload403()
    {
        var page = CreatePageModel();
        var form = UploadForm(fieldId: "docField");
        var executingContext = CreateExecutingContext(
            page,
            method: "POST",
            path: "/applications/REF-1/t1",
            form: form,
            handlerMethod: new HandlerMethodDescriptor { Name = "OnPostUploadFileAsync" });
        var response = new ExceptionResponse
        {
            StatusCode = 403,
            Message = "Forbidden",
            ErrorId = "151515"
        };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        Assert.IsType<PageResult>(executedContext.Result);
        Assert.Contains(
            page.ModelState["Error"]!.Errors,
            e => e.ErrorMessage == ApplicationAccessMessages.NoFileWritePermission);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ShouldRedirectForbidden_WhenDownloadFileAccessDenied403()
    {
        var page = CreatePageModel();
        var executingContext = CreateExecutingContext(
            page,
            method: "POST",
            path: "/applications/REF-1/t1",
            form: new FormCollection(new Dictionary<string, StringValues> { ["handler"] = "DownloadFile" }),
            handlerMethod: new HandlerMethodDescriptor { Name = "OnPostDownloadFileAsync" });
        var response = new ExceptionResponse
        {
            StatusCode = 403,
            Message = "Forbidden",
            ErrorId = "161616"
        };
        var executedContext = CreateExecutedContext(executingContext, CreateTypedApiException(response));

        await _pageFilter.OnPageHandlerExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        var redirect = Assert.IsType<RedirectToPageResult>(executedContext.Result);
        Assert.Equal("/Error/Forbidden", redirect.PageName);
        Assert.Equal(ApplicationAccessMessages.NoFileReadPermission, page.TempData["AccessDeniedReason"]);
    }

    #endregion

    #region ExternalApiMvcExceptionFilter

    [Fact]
    public void OnException_ShouldIgnoreNonApiExceptions()
    {
        var context = CreateExceptionContext(new InvalidOperationException("boom"));

        _mvcFilter.OnException(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public void OnException_ShouldReturnObjectResult_For401Or403(int statusCode)
    {
        var response = new ExceptionResponse
        {
            StatusCode = statusCode,
            Message = "Access denied",
            ErrorId = "aaaaaa",
            ExceptionType = "ForbiddenException"
        };
        var context = CreateExceptionContext(CreateTypedApiException(response));

        _mvcFilter.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(statusCode, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Access denied", problem.Title);
        Assert.Equal("aaaaaa", problem.Extensions["errorId"]);
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void OnException_ShouldReturnObjectResult_For429()
    {
        var response = new ExceptionResponse
        {
            StatusCode = 429,
            Message = "Rate limited",
            ErrorId = "bbbbbb"
        };
        var context = CreateExceptionContext(CreateTypedApiException(response));

        _mvcFilter.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(429, result.StatusCode);
        Assert.True(context.ExceptionHandled);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(409)]
    public void OnException_ShouldReturnBadRequestObjectResult_For400Or409(int statusCode)
    {
        var response = new ExceptionResponse
        {
            StatusCode = statusCode,
            Message = "Validation failed",
            ErrorId = "cccccc"
        };
        var context = CreateExceptionContext(CreateTypedApiException(response));

        _mvcFilter.OnException(context);

        var result = Assert.IsType<BadRequestObjectResult>(context.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(statusCode, problem.Status);
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void OnException_ShouldReturnObjectResult_ForOtherStatusCodes()
    {
        var response = new ExceptionResponse
        {
            StatusCode = 500,
            Message = "Server error",
            ErrorId = "dddddd",
            ExceptionType = "InternalServerError"
        };
        var context = CreateExceptionContext(CreateTypedApiException(response));

        _mvcFilter.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(500, result.StatusCode);
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void OnException_ShouldUseCorrelationHeader_WhenPresent()
    {
        var response = new ExceptionResponse
        {
            StatusCode = 500,
            Message = "Server error",
            ErrorId = "eeeeee",
            CorrelationId = "from-response"
        };
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[CorrelationIdForwardingHandler.HeaderName] = "from-header";
        var context = CreateExceptionContext(CreateTypedApiException(response), httpContext);

        _mvcFilter.OnException(context);

        Assert.True(context.ExceptionHandled);
        Assert.IsType<ObjectResult>(context.Result);
    }

    #endregion

    #region Helpers

    private sealed class TestPageModel : PageModel;

    private sealed class StubSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = Substitute.For<ISession>();
    }

    private static TestPageModel CreatePageModel(IServiceProvider? requestServices = null)
    {
        var httpContext = CreateHttpContext(requestServices);
        var pageContext = CreatePageContext(httpContext);

        var page = new TestPageModel { PageContext = pageContext };
        page.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
        return page;
    }

    private static PageContext CreatePageContext(HttpContext httpContext, ModelStateDictionary? modelState = null)
    {
        modelState ??= new ModelStateDictionary();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new CompiledPageActionDescriptor(),
            modelState);

        var pageContext = new PageContext(actionContext)
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState)
        };
        pageContext.RouteData = new RouteData();
        return pageContext;
    }

    private static DefaultHttpContext CreateHttpContext(IServiceProvider? requestServices = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = requestServices ?? new ServiceCollection().BuildServiceProvider();
        httpContext.Request.Headers[CorrelationIdForwardingHandler.HeaderName] = "corr-test";
        httpContext.Features.Set<ISessionFeature>(new StubSessionFeature { Session = Substitute.For<ISession>() });
        return httpContext;
    }

    private static PageHandlerExecutingContext CreateExecutingContext(
        TestPageModel page,
        string method = "POST",
        string path = "/page",
        string query = "",
        IFormCollection? form = null,
        HandlerMethodDescriptor? handlerMethod = null)
    {
        var httpContext = page.HttpContext;
        httpContext.Request.Method = method;
        httpContext.Request.Path = path;
        httpContext.Request.QueryString = new QueryString(query);
        if (form is not null)
        {
            httpContext.Request.ContentType = "application/x-www-form-urlencoded";
            httpContext.Features.Set<IFormFeature>(new FormFeature(form));
        }

        var pageContext = CreatePageContext(httpContext, page.ModelState);

        return new PageHandlerExecutingContext(
            pageContext,
            Array.Empty<IFilterMetadata>(),
            handlerMethod,
            new Dictionary<string, object?>(),
            page);
    }

    private static PageHandlerExecutedContext CreateExecutedContext(
        PageHandlerExecutingContext executingContext,
        Exception? exception = null)
    {
        var page = (TestPageModel)executingContext.HandlerInstance;
        var pageContext = CreatePageContext(executingContext.HttpContext, page.ModelState);
        var executed = new PageHandlerExecutedContext(
            pageContext,
            executingContext.Filters,
            executingContext.HandlerMethod,
            page)
        {
            Exception = exception
        };
        return executed;
    }

    private static ExternalApplicationsException CreatePlainApiException(int statusCode, string message) =>
        new(message, statusCode, message, new Dictionary<string, IEnumerable<string>>(), null);

    private static ExternalApplicationsException<ExceptionResponse> CreateTypedApiException(ExceptionResponse response) =>
        new(
            response.Message,
            response.StatusCode,
            response.Message,
            new Dictionary<string, IEnumerable<string>>(),
            response,
            null);

    private static ExceptionContext CreateExceptionContext(Exception exception, HttpContext? httpContext = null)
    {
        httpContext ??= CreateHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ExceptionContext(actionContext, Array.Empty<IFilterMetadata>())
        {
            Exception = exception
        };
    }

    private static FormCollection UploadForm(string fieldId, string? returnUrl = null)
    {
        var fields = new Dictionary<string, StringValues>
        {
            ["handler"] = "UploadFile",
            ["FieldId"] = fieldId
        };
        if (returnUrl is not null)
            fields["ReturnUrl"] = returnUrl;

        return new FormCollection(fields);
    }

    #endregion
}
