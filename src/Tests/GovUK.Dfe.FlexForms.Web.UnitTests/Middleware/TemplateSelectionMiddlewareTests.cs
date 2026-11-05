using System.Security.Claims;
using GovUK.Dfe.FlexForms.Web.Middleware;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Middleware;

public sealed class TemplateSelectionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SelectsSoleLiveTemplate_WhenStoredTemplateIsNotLive()
    {
        var liveTemplate = CreateTemplate("Live", isLive: true);
        var draftTemplate = CreateTemplate("Draft", isLive: false);
        var service = CreateService(liveTemplate, draftTemplate);
        service.GetSelectedTemplateId(Arg.Any<HttpContext>())
            .Returns(draftTemplate.TemplateId.ToString());
        service.IsPreviewSelection(Arg.Any<HttpContext>()).Returns(false);

        var nextCalled = false;
        var middleware = CreateMiddleware(() => nextCalled = true);
        var context = CreateUserContext("/applications/dashboard", liveTemplate, draftTemplate);

        await middleware.InvokeAsync(context, service);

        Assert.True(nextCalled);
        await service.Received(1).SelectTemplateAsync(
            context,
            liveTemplate,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_DoesNotAutoSelectSoleLiveTemplate_WhenTemplateManagerHasNoAccess()
    {
        var liveTemplate = CreateTemplate("Live", isLive: true);
        var service = CreateService(liveTemplate);
        var middleware = CreateMiddleware();
        var context = CreateTemplateManagerContext("/applications/dashboard");

        await middleware.InvokeAsync(context, service);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.StartsWith("/templates?liveOnly=true", context.Response.Headers.Location.ToString());
        await service.DidNotReceiveWithAnyArgs().SelectTemplateAsync(default!, default!, default);
    }

    [Fact]
    public async Task InvokeAsync_AutoSelectsTheOnlyLiveTemplateTheTemplateManagerHasAccessTo()
    {
        var granted = CreateTemplate("Granted", isLive: true);
        var other = CreateTemplate("Other", isLive: true);
        var service = CreateService(granted, other);
        var nextCalled = false;
        var middleware = CreateMiddleware(() => nextCalled = true);
        var context = CreateTemplateManagerContext("/applications/dashboard", granted);

        await middleware.InvokeAsync(context, service);

        Assert.True(nextCalled);
        await service.Received(1).SelectTemplateAsync(context, granted, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_SelectsTheTemplate_ForACaseworkerWithoutTemplateGrants()
    {
        var liveTemplate = CreateTemplate("Live", isLive: true);
        var service = CreateService(liveTemplate);
        var nextCalled = false;
        var middleware = CreateMiddleware(() => nextCalled = true);
        var context = CreateAuthenticatedContext("/applications/dashboard");
        context.User.AddIdentity(new ClaimsIdentity([new Claim("permission", "Application:Any:Read")]));

        await middleware.InvokeAsync(context, service);

        Assert.True(nextCalled);
        await service.Received(1).SelectTemplateAsync(context, liveTemplate, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_SelectsTheTemplate_ForATemplateManagerWhoCanAlsoReadEveryApplication()
    {
        var liveTemplate = CreateTemplate("Live", isLive: true);
        var service = CreateService(liveTemplate);
        var nextCalled = false;
        var middleware = CreateMiddleware(() => nextCalled = true);
        var context = CreateTemplateManagerContext("/applications/dashboard");
        context.User.AddIdentity(new ClaimsIdentity([new Claim("permission", "Application:Any:Read")]));

        await middleware.InvokeAsync(context, service);

        Assert.True(nextCalled);
        await service.Received(1).SelectTemplateAsync(context, liveTemplate, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_IgnoresQueryTemplate_WhenTemplateManagerHasNoAccess()
    {
        var granted = CreateTemplate("Granted", isLive: true);
        var other = CreateTemplate("Other", isLive: true);
        var service = CreateService(granted, other);
        var middleware = CreateMiddleware();
        var context = CreateTemplateManagerContext("/applications/dashboard", granted);
        context.Request.QueryString = new QueryString($"?templateId={other.TemplateId}");

        await middleware.InvokeAsync(context, service);

        await service.DidNotReceive().SelectTemplateAsync(context, other, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_RedirectsRootToSoleLiveTemplateDashboard()
    {
        var liveTemplate = CreateTemplate("Live", isLive: true);
        var service = CreateService(liveTemplate);
        var middleware = CreateMiddleware();
        var context = CreateUserContext("/", liveTemplate);

        await middleware.InvokeAsync(context, service);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/applications/dashboard", context.Response.Headers.Location.ToString());
        await service.Received(1).SelectTemplateAsync(
            context,
            liveTemplate,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_RedirectsToLiveChooser_WhenMultipleLiveTemplatesExist()
    {
        var service = CreateService(
            CreateTemplate("One", isLive: true),
            CreateTemplate("Two", isLive: true),
            CreateTemplate("Draft", isLive: false));
        var middleware = CreateMiddleware();
        var context = CreateAuthenticatedContext("/applications/dashboard");

        await middleware.InvokeAsync(context, service);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.StartsWith("/templates?liveOnly=true", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task InvokeAsync_AllowsDashboard_WhenMultipleLiveTemplatesAndValidSelection()
    {
        var one = CreateTemplate("One", isLive: true);
        var two = CreateTemplate("Two", isLive: true);
        var service = CreateService(one, two);
        service.GetSelectedTemplateId(Arg.Any<HttpContext>())
            .Returns(two.TemplateId.ToString());

        var nextCalled = false;
        var middleware = CreateMiddleware(() => nextCalled = true);
        var context = CreateUserContext("/applications/dashboard", one, two);

        await middleware.InvokeAsync(context, service);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status302Found, context.Response.StatusCode);
        await service.Received(1).SelectTemplateAsync(
            context,
            two,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_AllowsExplicitNonLivePreview_ForAdmin()
    {
        var liveTemplate = CreateTemplate("Live", isLive: true);
        var draftTemplate = CreateTemplate("Draft", isLive: false);
        var service = CreateService(liveTemplate, draftTemplate);
        service.GetSelectedTemplateId(Arg.Any<HttpContext>())
            .Returns(draftTemplate.TemplateId.ToString());
        service.IsPreviewSelection(Arg.Any<HttpContext>()).Returns(true);

        var nextCalled = false;
        var middleware = CreateMiddleware(() => nextCalled = true);
        var context = CreateAuthenticatedContext("/applications/dashboard", "Admin");

        await middleware.InvokeAsync(context, service);

        Assert.True(nextCalled);
        await service.Received(1).SelectTemplateAsync(
            context,
            draftTemplate,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_RedirectsAdminToCreateTemplate_WhenNoTemplatesExist()
    {
        var service = CreateService();
        var middleware = CreateMiddleware();
        var context = CreateAuthenticatedContext("/applications/dashboard", "Admin");

        await middleware.InvokeAsync(context, service);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/admin/create-template", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task InvokeAsync_RedirectsNonAdminToTemplates_WhenNoTemplatesExist()
    {
        var service = CreateService();
        var middleware = CreateMiddleware();
        var context = CreateAuthenticatedContext("/applications/dashboard");

        await middleware.InvokeAsync(context, service);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.StartsWith("/templates?liveOnly=true", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task InvokeAsync_RedirectsToLiveChooser_WhenOnlyDraftTemplatesExist()
    {
        var service = CreateService(CreateTemplate("Draft", isLive: false));
        var middleware = CreateMiddleware();
        var context = CreateAuthenticatedContext("/applications/dashboard", "Admin");

        await middleware.InvokeAsync(context, service);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.StartsWith("/templates?liveOnly=true", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task InvokeAsync_DoesNotSwallowExceptionsFromDownstreamPage()
    {
        var one = CreateTemplate("One", isLive: true);
        var two = CreateTemplate("Two", isLive: true);
        var service = CreateService(one, two);
        service.GetSelectedTemplateId(Arg.Any<HttpContext>())
            .Returns(one.TemplateId.ToString());

        var middleware = CreateMiddleware(() => throw new InvalidOperationException("dashboard broke"));
        var context = CreateUserContext("/applications/dashboard", one, two);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context, service));

        Assert.Equal("dashboard broke", ex.Message);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task InvokeAsync_RedirectsToChooser_WhenGateItselfFails()
    {
        var service = Substitute.For<ITemplateSelectionService>();
        service.GetSelectableTemplatesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<TemplateDto>>(new HttpRequestException("api down")));

        var nextCalled = false;
        var middleware = CreateMiddleware(() => nextCalled = true);
        var context = CreateAuthenticatedContext("/applications/dashboard");

        await middleware.InvokeAsync(context, service);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/templates", context.Response.Headers.Location.ToString());
    }

    private static TemplateSelectionMiddleware CreateMiddleware(Action? onNext = null)
        => new(
            _ =>
            {
                onNext?.Invoke();
                return Task.CompletedTask;
            },
            NullLogger<TemplateSelectionMiddleware>.Instance);

    private static ITemplateSelectionService CreateService(params TemplateDto[] templates)
    {
        var service = Substitute.For<ITemplateSelectionService>();
        service.GetSelectableTemplatesAsync(Arg.Any<CancellationToken>())
            .Returns(templates.ToList());
        service.SelectTemplateAsync(
                Arg.Any<HttpContext>(),
                Arg.Any<TemplateDto>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return service;
    }

    private static DefaultHttpContext CreateAuthenticatedContext(string path, params string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role));
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            Request = { Path = path }
        };
        return context;
    }

    private static DefaultHttpContext CreateUserContext(string path, params TemplateDto[] accessibleTemplates)
    {
        var context = CreateAuthenticatedContext(path);
        context.User.AddIdentity(new ClaimsIdentity(
            accessibleTemplates.Select(t => new Claim("permission", $"Template:{t.TemplateId}:Write"))));
        return context;
    }

    private static DefaultHttpContext CreateTemplateManagerContext(string path, params TemplateDto[] accessibleTemplates)
    {
        var context = CreateUserContext(path, accessibleTemplates);
        context.User.AddIdentity(new ClaimsIdentity([new Claim("permission", "Template:Any:Manage")]));
        return context;
    }

    private static TemplateDto CreateTemplate(string name, bool isLive)
        => new()
        {
            TemplateId = Guid.NewGuid(),
            Name = name,
            CreatedOn = DateTime.UtcNow,
            IsLive = isLive
        };
}
