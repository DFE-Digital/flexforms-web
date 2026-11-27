using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages;

/// <summary>Who is signed in, as the claims Web builds from the API's roles and permissions.</summary>
internal static class TestPrincipals
{
    public static ClaimsPrincipal Admin() => WithRoles("Admin");

    public static ClaimsPrincipal SuperAdmin() => WithRoles("SuperAdmin");

    public static ClaimsPrincipal User() => WithRoles("User");

    /// <summary>A template manager (<c>Template:Any:Manage</c>) given only <paramref name="grantedTemplateIds"/>.</summary>
    public static ClaimsPrincipal TemplateManager(params Guid[] grantedTemplateIds) => Build(
        [],
        ["Template:Any:Manage", .. grantedTemplateIds.Select(id => $"Template:{id}:Write")]);

    public static ClaimsPrincipal UserManager() => Build([], ["User:Any:Manage"]);

    private static ClaimsPrincipal WithRoles(params string[] roles) => Build(roles, []);

    private static ClaimsPrincipal Build(IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, "actor@tenant.example") };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}

/// <summary>Gives a PageModel an HTTP context, session, TempData and request services for unit tests.</summary>
internal sealed class PageModelTestContext
{
    private PageModelTestContext(DefaultHttpContext httpContext, TestSession session, ITempDataDictionary tempData)
    {
        HttpContext = httpContext;
        Session = session;
        TempData = tempData;
    }

    public DefaultHttpContext HttpContext { get; }

    public TestSession Session { get; }

    public ITempDataDictionary TempData { get; }

    public static PageModelTestContext Attach(
        PageModel model,
        ClaimsPrincipal user,
        Action<IServiceCollection>? services = null)
    {
        var collection = new ServiceCollection();
        services?.Invoke(collection);
        var session = new TestSession();
        var httpContext = new DefaultHttpContext
        {
            User = user,
            RequestServices = collection.BuildServiceProvider()
        };
        httpContext.Features.Set<ISessionFeature>(new SessionFeature { Session = session });
        var tempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());

        model.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
        model.TempData = tempData;
        var url = Substitute.For<IUrlHelper>();
        url.IsLocalUrl(Arg.Any<string?>()).Returns(call =>
            call.Arg<string?>() is { } value && value.StartsWith('/') && !value.StartsWith("//") && !value.StartsWith("/\\"));
        model.Url = url;
        return new PageModelTestContext(httpContext, session, tempData);
    }

    /// <summary>
    /// Request services for <c>TenantAuthSchemeSelector</c>: the host environment, and the tenant's
    /// <c>TestAuthentication:Enabled</c> setting (left out when <paramref name="tenantEnablesTestAuth"/> is null).
    /// </summary>
    public static Action<IServiceCollection> TestAuthServices(string environmentName, bool? tenantEnablesTestAuth) => services =>
    {
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(environmentName));
        var settings = new Dictionary<string, string?>();
        if (tenantEnablesTestAuth is { } enabled)
            settings["TestAuthentication:Enabled"] = enabled.ToString();
        services.AddScoped<ITenantRequestContext>(_ => new TenantRequestContext
        {
            TenantId = Guid.Parse("11111111-0000-4000-8000-000000000001"),
            TenantConfiguration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build()
        });
    };

    private sealed class SessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession();
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "GovUK.Dfe.FlexForms.Web";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

internal sealed class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = [];

    public bool IsAvailable => true;

    public string Id => "test-session";

    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Remove(string key) => _store.Remove(key);

    public void Set(string key, byte[] value) => _store[key] = value;

    public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => _store.TryGetValue(key, out value);
}
