using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using Microsoft.AspNetCore.Http;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services.Tenant;

public class TenantIdResolverHostnameTests
{
    [Fact]
    public void ResolvePublicHostname_PrefersRequestHost_WhenOriginalHostIsContainerAppsFqdn()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("visits.dev-flexforms.rsd.education.gov.uk");
        context.Request.Headers["X-Original-Host"] =
            "s184d01-extapp-flexformsweb.agreeabledune-8224bf9e.westeurope.azurecontainerapps.io";

        var host = TenantIdResolver.ResolvePublicHostname(context);

        Assert.Equal("visits.dev-flexforms.rsd.education.gov.uk", host);
    }

    [Fact]
    public void ResolvePublicHostname_PrefersForwardedHost_WhenPresent()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("s184d01-extapp-flexformsweb.agreeabledune-8224bf9e.westeurope.azurecontainerapps.io");
        context.Request.Headers["X-Forwarded-Host"] = "visits.dev-flexforms.rsd.education.gov.uk";
        context.Request.Headers["X-Original-Host"] =
            "s184d01-extapp-flexformsweb.agreeabledune-8224bf9e.westeurope.azurecontainerapps.io";

        var host = TenantIdResolver.ResolvePublicHostname(context);

        Assert.Equal("visits.dev-flexforms.rsd.education.gov.uk", host);
    }

    [Fact]
    public void ResolvePublicHostname_RejectsContainerAppsFqdnAlone()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(
            "s184d01-extapp-flexformsweb.agreeabledune-8224bf9e.westeurope.azurecontainerapps.io");

        var host = TenantIdResolver.ResolvePublicHostname(context);

        Assert.Null(host);
        Assert.True(TenantIdResolver.IsNonPublicHostRequest(context));
    }

    [Fact]
    public void HasConflictingPublicHostnames_IsTrue_WhenForwardedAndRequestHostDiffer()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("a.example.gov.uk");
        context.Request.Headers["X-Forwarded-Host"] = "b.example.gov.uk";

        Assert.True(TenantIdResolver.HasConflictingPublicHostnames(context));
    }

    [Fact]
    public void IsNonPublicHostRequest_IsFalse_ForPublicTenantHost()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("visits.flexforms.rsd.education.gov.uk");

        Assert.False(TenantIdResolver.IsNonPublicHostRequest(context));
    }

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.1.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fec0::1")]
    [InlineData("fd12:3456:789a:1::1")]
    public void ResolvePublicHostname_RejectsPrivateAndLocalIpHosts(string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);

        Assert.Null(TenantIdResolver.ResolvePublicHostname(context));
        Assert.True(TenantIdResolver.IsNonPublicHostRequest(context));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.15.0.1")]
    [InlineData("172.32.0.1")]
    [InlineData("2001:4860:4860::8888")]
    public void ResolvePublicHostname_AcceptsPublicIpHosts(string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);

        Assert.Equal(host, TenantIdResolver.ResolvePublicHostname(context));
    }

    [Fact]
    public void ResolvePublicHostname_AcceptsLocalhostAndDotLocalhost()
    {
        var localhost = new DefaultHttpContext();
        localhost.Request.Host = new HostString("localhost");
        Assert.Equal("localhost", TenantIdResolver.ResolvePublicHostname(localhost));

        var dotted = new DefaultHttpContext();
        dotted.Request.Host = new HostString("visits.localhost");
        Assert.Equal("visits.localhost", TenantIdResolver.ResolvePublicHostname(dotted));
    }

    [Fact]
    public void ResolvePublicHostname_UsesLeftmostForwardedHost_AndStripsPort()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("backend.azurecontainerapps.io");
        context.Request.Headers["X-Forwarded-Host"] = "visits.example.gov.uk:443, proxy.internal";

        Assert.Equal("visits.example.gov.uk", TenantIdResolver.ResolvePublicHostname(context));
    }

    [Fact]
    public void HasConflictingPublicHostnames_IsFalse_WhenHostsMatchIgnoringCase()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("Visits.Example.Gov.Uk");
        context.Request.Headers["X-Forwarded-Host"] = "visits.example.gov.uk";

        Assert.False(TenantIdResolver.HasConflictingPublicHostnames(context));
    }
}
