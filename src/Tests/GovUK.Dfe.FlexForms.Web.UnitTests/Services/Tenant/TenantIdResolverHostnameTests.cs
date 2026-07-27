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
    }
}
