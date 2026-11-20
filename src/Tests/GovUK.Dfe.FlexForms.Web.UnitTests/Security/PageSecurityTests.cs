using System.Reflection;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Razor.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

/// <summary>
/// Pins who can reach every Razor page and controller action. The effective rule is worked out the way
/// ASP.NET Core does it: <see cref="PageAccessConventions"/> applied to the page, plus the attributes on
/// the page, its PageModel or the controller and action. Anonymous wins over everything else.
/// A new page or action, or a changed rule, fails here until <c>Security/ExpectedPageSecurity.json</c> is updated.
/// </summary>
public sealed class PageSecurityTests
{
    private const string ExpectedFile = "Security/ExpectedPageSecurity.json";
    private const string Missing = "<no row in ExpectedPageSecurity.json>";
    private const string Gone = "<no such page or action>";

    private static readonly Assembly WebAssembly = typeof(PageAccessConventions).Assembly;

    public static TheoryData<string> Endpoints()
    {
        var keys = new TheoryData<string>();
        foreach (var key in Expected().Keys.Union(Actual(Environments.Production).Keys).Order(StringComparer.Ordinal))
            keys.Add(key);
        return keys;
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public void EveryPageAndActionHasTheExpectedAccessRule(string endpoint)
    {
        var expected = Expected().GetValueOrDefault(endpoint, Missing);
        var actual = Actual(Environments.Production).GetValueOrDefault(endpoint, Gone);

        Assert.True(
            expected == actual,
            $"{endpoint}: ExpectedPageSecurity.json says \"{expected}\" but the code gives \"{actual}\". "
            + "If the change is intended, update the file in the same pull request.");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    [InlineData("Staging")]
    public void OutsideProduction_OnlyTheTestErrorPageBecomesAnonymous(string environment)
    {
        var production = Actual(Environments.Production);
        var other = Actual(environment);

        var changed = production.Keys.Where(key => production[key] != other[key]).ToList();

        Assert.Equal(["Page /TestError"], changed);
        Assert.Equal(Anonymous, other["Page /TestError"]);
    }

    [Fact]
    public void ExpectedFile_HasNoDuplicateRows()
    {
        var rows = ReadRows();

        Assert.Equal(rows.Count, rows.Select(r => r.Key).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void FindsThePagesAndControllers()
    {
        // Guards the discovery itself: an empty result would make every row above "gone" rather than pass quietly.
        var actual = Actual(Environments.Production);

        Assert.Contains("Page /Admin/Admin", actual.Keys);
        Assert.Contains("Page /Shared/PrivacyNotice", actual.Keys);
        Assert.Contains("Controller NotificationsController.GetUnreadAsync", actual.Keys);
        Assert.True(actual.Count(k => k.Key.StartsWith("Page ", StringComparison.Ordinal)) >= 50);
    }


    private const string Anonymous = "Anonymous";

    private static Dictionary<string, string> Expected() =>
        ReadRows().ToDictionary(r => r.Key, r => r.ExpectedSecurity, StringComparer.Ordinal);

    private static List<(string Key, string ExpectedSecurity)> ReadRows()
    {
        var path = Path.Combine(AppContext.BaseDirectory, ExpectedFile);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var rows = new List<(string, string)>();

        foreach (var page in document.RootElement.GetProperty("Pages").EnumerateArray())
            rows.Add(($"Page {page.GetProperty("Page").GetString()}", page.GetProperty("ExpectedSecurity").GetString()!));

        foreach (var action in document.RootElement.GetProperty("Controllers").EnumerateArray())
        {
            rows.Add((
                $"Controller {action.GetProperty("Controller").GetString()}.{action.GetProperty("Action").GetString()}",
                action.GetProperty("ExpectedSecurity").GetString()!));
        }

        return rows;
    }

    private static Dictionary<string, string> Actual(string environmentName)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (path, model) in PageModels(environmentName))
        {
            // Page attributes and convention metadata both end up on the endpoint.
            result[$"Page {path}"] = Describe(model.HandlerTypeAttributes.Concat(model.EndpointMetadata));
        }

        foreach (var controller in Controllers())
        {
            var controllerAttributes = controller.GetCustomAttributes(inherit: true);
            foreach (var action in Actions(controller))
            {
                result[$"Controller {controller.Name}.{action.Name}"] =
                    Describe(controllerAttributes.Concat(action.GetCustomAttributes(inherit: true)));
            }
        }

        return result;
    }

    private static IEnumerable<(string Path, PageApplicationModel Model)> PageModels(string environmentName)
    {
        var environment = new TestEnvironment(environmentName);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment>(environment);
        services.AddRazorPages(options => PageAccessConventions.Apply(options.Conventions, environment));
        using var provider = services.BuildServiceProvider();
        var conventions = provider.GetRequiredService<IOptions<RazorPagesOptions>>().Value.Conventions
            .OfType<IPageApplicationModelConvention>()
            .ToList();

        foreach (var item in new RazorCompiledItemLoader().LoadItems(WebAssembly))
        {
            if (item.Kind != "mvc.1.0.razor-page" || !item.Identifier.StartsWith("/Pages/", StringComparison.Ordinal))
                continue;

            var path = item.Identifier["/Pages".Length..^".cshtml".Length];
            var modelType = item.Type.GetProperty("Model")?.PropertyType;
            var handlerType = modelType is not null && typeof(PageModel).IsAssignableFrom(modelType)
                ? modelType
                : item.Type;

            var descriptor = new PageActionDescriptor
            {
                RelativePath = item.Identifier,
                ViewEnginePath = path,
                RouteValues = new Dictionary<string, string?> { ["page"] = path }
            };
            var attributes = handlerType.GetCustomAttributes(inherit: true)
                .Concat(handlerType == item.Type ? [] : item.Type.GetCustomAttributes(inherit: true))
                .ToList();
            var model = new PageApplicationModel(descriptor, handlerType.GetTypeInfo(), attributes);

            foreach (var convention in conventions)
                convention.Apply(model);

            yield return (path, model);
        }
    }

    private static IEnumerable<Type> Controllers() =>
        WebAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true }
                && typeof(ControllerBase).IsAssignableFrom(t)
                && t.GetCustomAttribute<NonControllerAttribute>() is null)
            .OrderBy(t => t.Name, StringComparer.Ordinal);

    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null)
            .OrderBy(m => m.Name, StringComparer.Ordinal);

    /// <summary>
    /// "Anonymous" when anything allows anonymous access (it wins); otherwise every requirement, sorted
    /// and joined with " + " because they all apply; "None" when nothing protects it.
    /// </summary>
    private static string Describe(IEnumerable<object> metadata)
    {
        var items = metadata.ToList();
        if (items.OfType<IAllowAnonymous>().Any())
            return Anonymous;

        var requirements = items.OfType<IAuthorizeData>()
            .SelectMany(Requirements)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return requirements.Count == 0 ? "None" : string.Join(" + ", requirements);
    }

    private static IEnumerable<string> Requirements(IAuthorizeData data)
    {
        var any = false;
        if (!string.IsNullOrEmpty(data.Policy))
        {
            any = true;
            yield return $"Policy:{data.Policy}";
        }

        if (!string.IsNullOrEmpty(data.Roles))
        {
            any = true;
            yield return $"Roles:{data.Roles}";
        }

        if (!string.IsNullOrEmpty(data.AuthenticationSchemes))
        {
            any = true;
            yield return $"Schemes:{data.AuthenticationSchemes}";
        }

        if (!any)
            yield return "SignedIn";
    }

    private sealed class TestEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "GovUK.Dfe.FlexForms.Web";
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
