using System.Security.Claims;
using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.CoreLibs.Security.Authorization;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Constants;
using GovUK.Dfe.FlexForms.Web.Middleware;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Telemetry;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Middleware;

public sealed class PermissionsCacheMiddlewareTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Dictionary<string, IEnumerable<string>> NoHeaders = new();

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly IUsersClient _usersClient = Substitute.For<IUsersClient>();
    private readonly IAuthenticationService _authentication = Substitute.For<IAuthenticationService>();
    private readonly ITempDataProvider _tempDataProvider = Substitute.For<ITempDataProvider>();
    private bool _nextCalled;

    [Fact]
    public async Task SkippedPath_DoesNotFetchPermissions()
    {
        var context = CreateContext(SignedIn(), "/health");

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        await _usersClient.DidNotReceiveWithAnyArgs().GetMyPermissionsAsync(default);
    }

    [Fact]
    public async Task SignedOutRequest_DoesNotFetchPermissions()
    {
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()));

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        await _usersClient.DidNotReceiveWithAnyArgs().GetMyPermissionsAsync(default);
    }

    [Fact]
    public async Task SignedInUserWithNoUserId_DoesNotFetchPermissions()
    {
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, "a@b.c")], "Cookies")));

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        await _usersClient.DidNotReceiveWithAnyArgs().GetMyPermissionsAsync(default);
    }

    [Fact]
    public async Task SignedInUser_GetsPermissionClaimsRebuiltFromTheApi()
    {
        var user = SignedIn(new Claim("permission", "Template:stale:Read"));
        var context = CreateContext(user);
        _usersClient.GetMyPermissionsAsync(Arg.Any<CancellationToken>()).Returns(new UserAuthorizationDto
        {
            Roles = ["Caseworker"],
            Permissions =
            [
                new UserPermissionDto
                {
                    ResourceType = ResourceType.Application,
                    ResourceKey = "Any",
                    AccessType = AccessType.Read
                }
            ]
        });

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        Assert.DoesNotContain(context.User.Claims, c => c.Value == "Template:stale:Read");
        Assert.Contains(context.User.Claims, c => c.Type == "permission" && c.Value == "Application:Any:Read");
        Assert.True(context.User.IsInRole("Caseworker"));
    }

    [Fact]
    public async Task PermissionsApiUnavailable_CarriesOnWithNoPermissionClaims()
    {
        // UserPermissionsCache.RefreshAsync logs and swallows non-auth failures, so a stale grant
        // must not survive: the old claims are removed and nothing replaces them.
        var context = CreateContext(SignedIn(new Claim("permission", "Application:Any:Read")));
        _usersClient.GetMyPermissionsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        Assert.DoesNotContain(context.User.Claims, c => c.Type == "permission");
    }

    [Fact]
    public async Task NoTenantMembership_SignsOutTheCookieAndShowsForbiddenWithTheReason()
    {
        var context = CreateContext(SignedIn());
        var body = JsonSerializer.Serialize(new ExceptionResponse
        {
            StatusCode = 403,
            ErrorId = TenantMembershipDenial.ErrorId,
            Message = "Denied"
        });
        _usersClient.GetMyPermissionsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ExternalApplicationsException("API error", 403, body, NoHeaders, null));

        await InvokeAsync(context);

        Assert.False(_nextCalled);
        Assert.Equal("/Error/Forbidden", context.Response.Headers.Location.ToString());
        await _authentication.Received(1).SignOutAsync(
            context, CookieAuthenticationDefaults.AuthenticationScheme, Arg.Any<AuthenticationProperties?>());
        // Only the local cookie: the identity provider session is left alone.
        await _authentication.Received(1).SignOutAsync(
            Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>());
        _tempDataProvider.Received(1).SaveTempData(
            context,
            Arg.Is<IDictionary<string, object>>(values =>
                (string)values["AccessDeniedReason"] == ApplicationAccessMessages.NotAuthorisedForTenant
                && (string)values["ApiErrorId"] == TenantMembershipDenial.ErrorId));
    }

    [Fact]
    public async Task Unauthorised_LogsOutAsAuthFailure()
    {
        var context = CreateContext(SignedIn());
        _usersClient.GetMyPermissionsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ExternalApplicationsException("Unauthorized", 401, "", NoHeaders, null));

        await InvokeAsync(context);

        Assert.False(_nextCalled);
        Assert.Equal("/Logout?reason=auth_failed", context.Response.Headers.Location.ToString());
        await _authentication.DidNotReceiveWithAnyArgs().SignOutAsync(default!, default, default);
    }

    [Fact]
    public async Task ForbiddenBecauseTheTokenExpired_LogsOutAsTokenExpired()
    {
        var context = CreateContext(SignedIn());
        _usersClient.GetMyPermissionsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ExternalApplicationsException("The token has expired", 403, "", NoHeaders, null));

        await InvokeAsync(context);

        Assert.False(_nextCalled);
        Assert.Equal("/Logout?reason=token_expired", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task OtherForbidden_CarriesOn()
    {
        var context = CreateContext(SignedIn());
        _usersClient.GetMyPermissionsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ExternalApplicationsException("API error", 403, "{\"message\":\"Role required\"}", NoHeaders, null));

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        await _authentication.DidNotReceiveWithAnyArgs().SignOutAsync(default!, default, default);
    }

    private async Task InvokeAsync(HttpContext context)
    {
        var middleware = new PermissionsCacheMiddleware(
            _ =>
            {
                _nextCalled = true;
                return Task.CompletedTask;
            },
            _cache,
            _usersClient,
            new ReportingUserIdGenerator(null));

        await middleware.InvokeAsync(context);
    }

    private DefaultHttpContext CreateContext(ClaimsPrincipal user, string path = "/applications/dashboard")
    {
        var accessor = new HttpContextAccessor();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_authentication);
        services.AddSingleton<ITenantRequestContext>(new TenantRequestContext { TenantId = TenantId });
        services.AddSingleton<ITempDataDictionaryFactory>(new TempDataDictionaryFactory(_tempDataProvider));
        services.AddSingleton<IClaimsTransformation>(
            new CustomClaimsTransformation([new PermissionsClaimProvider(_cache, accessor)]));

        var context = new DefaultHttpContext { User = user, RequestServices = services.BuildServiceProvider() };
        context.Request.Path = path;
        accessor.HttpContext = context;
        return context;
    }

    private static ClaimsPrincipal SignedIn(params Claim[] extraClaims) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(ClaimTypes.Email, "user@example.com"), .. extraClaims],
            "Cookies"));
}
