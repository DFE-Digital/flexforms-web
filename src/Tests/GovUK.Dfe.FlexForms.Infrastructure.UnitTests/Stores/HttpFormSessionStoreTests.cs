using GovUK.Dfe.CoreLibs.Testing.Mocks.Session;
using GovUK.Dfe.FlexForms.Infrastructure.Stores;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Stores;

public class HttpFormSessionStoreTests
{
    [Fact]
    public void GetString_SetString_Remove_round_trip_http_session()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Session = new InMemorySession();
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var store = new HttpFormSessionStore(accessor);

        store.SetString("TemplateId", "abc");
        Assert.Equal("abc", store.GetString("TemplateId"));
        Assert.Contains("TemplateId", store.Keys);

        store.Remove("TemplateId");
        Assert.Null(store.GetString("TemplateId"));
        Assert.DoesNotContain("TemplateId", store.Keys);
    }

    [Fact]
    public void GetString_throws_when_http_context_is_missing()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var store = new HttpFormSessionStore(accessor);

        Assert.Throws<InvalidOperationException>(() => store.GetString("any"));
    }
}
