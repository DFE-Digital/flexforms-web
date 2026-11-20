using System.Security.Claims;
using GovUK.Dfe.FlexForms.Web.Middleware;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Middleware;

public sealed class TenantSessionBindingMiddlewareTests
{
    private static readonly Guid HostTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenant = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IAuthenticationService _authentication = Substitute.For<IAuthenticationService>();
    private bool _nextCalled;

    [Fact]
    public async Task SignedOutRequest_CarriesOn()
    {
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()));

        await Invoke(context, HostTenant);

        await AssertCarriedOnAsync(context);
    }

    [Fact]
    public async Task SessionForTheHostTenant_CarriesOn()
    {
        var context = CreateContext(SignedIn(HostTenant.ToString()));

        await Invoke(context, HostTenant);

        await AssertCarriedOnAsync(context);
    }

    [Fact]
    public async Task SessionWithNoTenantClaim_CarriesOn()
    {
        // Cookies issued before the claim existed are allowed until the user next signs in.
        var context = CreateContext(SignedIn(tenantClaim: null));

        await Invoke(context, HostTenant);

        await AssertCarriedOnAsync(context);
    }

    [Fact]
    public async Task TenantClaimThatIsNotAGuid_CarriesOn()
    {
        var context = CreateContext(SignedIn("not-a-guid"));

        await Invoke(context, HostTenant);

        await AssertCarriedOnAsync(context);
    }

    [Fact]
    public async Task NoTenantResolvedForTheHost_CarriesOn()
    {
        var context = CreateContext(SignedIn(OtherTenant.ToString()));

        await Invoke(context, resolvedTenant: null);

        await AssertCarriedOnAsync(context);
    }

    [Fact]
    public async Task SessionForAnotherTenant_IsSignedOutAndSentToForbidden()
    {
        var context = CreateContext(SignedIn(OtherTenant.ToString()));

        await Invoke(context, HostTenant);

        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/Error/Forbidden", context.Response.Headers.Location.ToString());
        await _authentication.Received(1).SignOutAsync(
            context,
            CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Any<AuthenticationProperties?>());
    }

    private async Task Invoke(HttpContext context, Guid? resolvedTenant)
    {
        var middleware = new TenantSessionBindingMiddleware(
            _ =>
            {
                _nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<TenantSessionBindingMiddleware>.Instance);

        await middleware.InvokeAsync(context, new TenantRequestContext { TenantId = resolvedTenant });
    }

    private async Task AssertCarriedOnAsync(HttpContext context)
    {
        Assert.True(_nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        await _authentication.DidNotReceiveWithAnyArgs().SignOutAsync(default!, default, default);
    }

    private DefaultHttpContext CreateContext(ClaimsPrincipal user)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_authentication);
        return new DefaultHttpContext { User = user, RequestServices = services.BuildServiceProvider() };
    }

    private static ClaimsPrincipal SignedIn(string? tenantClaim)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-1") };
        if (tenantClaim is not null)
            claims.Add(new Claim(TenantSessionBindingMiddleware.TenantIdClaimType, tenantClaim));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
    }
}
