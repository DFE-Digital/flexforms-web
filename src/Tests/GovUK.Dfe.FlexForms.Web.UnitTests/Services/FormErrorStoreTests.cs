using GovUK.Dfe.CoreLibs.Testing.Mocks.Session;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class FormErrorStoreTests
{
    [Fact]
    public void SaveAndLoad_ShouldRoundTripFieldAndGeneralErrors()
    {
        var session = new InMemorySession();
        var http = new DefaultHttpContext { Session = session };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(http);
        var store = new FormErrorStore(accessor);
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", "Enter a name");

        store.Save("page1", modelState, "Something went wrong");
        var loaded = store.Load("page1");

        Assert.Equal("Enter a name", loaded.FieldErrors["name"].Single());
        Assert.Equal("Something went wrong", loaded.GeneralError);
        Assert.Null(session.GetString("FormErrors_page1"));
    }

    [Fact]
    public void Load_ShouldKeepErrors_WhenClearAfterReadIsFalse()
    {
        var session = new InMemorySession();
        var http = new DefaultHttpContext { Session = session };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(http);
        var store = new FormErrorStore(accessor);
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", "Enter a name");
        store.Save("page1", modelState);

        store.Load("page1", clearAfterRead: false);

        Assert.NotNull(session.GetString("FormErrors_page1"));
    }

    [Fact]
    public void Save_ShouldThrow_WhenHttpContextMissing()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var store = new FormErrorStore(accessor);

        Assert.Throws<InvalidOperationException>(() => store.Save("x", new ModelStateDictionary()));
    }
}
