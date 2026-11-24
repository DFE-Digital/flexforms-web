using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
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
using Microsoft.FeatureManagement.Mvc;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

/// <summary>
/// Pins who can reach every Razor page and controller action. The effective rule is worked out the way
/// ASP.NET Core does it: <see cref="PageAccessConventions"/> applied to the page, plus the attributes on
/// the page, its PageModel or the controller and action. Anonymous wins over every authorization rule;
/// a <c>[FeatureGate]</c> is listed as well because it still hides the page when the feature is off.
/// Minimal APIs are read from the <c>app.Map…</c> calls in <c>Program.cs</c>.
/// A new page, action or minimal API, or a changed rule, fails here until <c>Security/ExpectedPageSecurity.json</c> is updated.
/// </summary>
public sealed class PageSecurityTests
{
    private const string ExpectedFile = "Security/ExpectedPageSecurity.json";
    private const string ProgramFile = "Security/WebProgram.cs.txt";
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
        Assert.Contains("Endpoint GET /health", actual.Keys);
        Assert.True(actual.Count(k => k.Key.StartsWith("Page ", StringComparison.Ordinal)) >= 50);
    }

    [Fact]
    public void FeatureGatedPages_ShowTheGate()
    {
        var actual = Actual(Environments.Production);

        Assert.StartsWith("FeatureGate:TemplateStudio + ", actual["Page /Admin/TemplateStudio"], StringComparison.Ordinal);
        Assert.StartsWith("FeatureGate:TemplateStudio + ", actual["Page /Admin/TemplateStudioPreview"], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("app.MapGet(\"/a\", () => Results.Ok());", "GET /a", "None")]
    [InlineData("app.MapPost(\"/a\", (string x) => Results.Ok(x)).RequireAuthorization(\"CanX\");", "POST /a", "Policy:CanX")]
    [InlineData("app.MapGet(\"/a\", c => { c.Response.Redirect(\"/b\"); return Task.CompletedTask; })\n    .RequireAuthorization();", "GET /a", "SignedIn")]
    [InlineData("app.Map(\"/a\", () => 1).AllowAnonymous().ShortCircuit();", "ANY /a", "Anonymous + ShortCircuit")]
    [InlineData("// app.MapGet(\"/old\", () => 1);\napp.MapGet(\"/a\", () => 1).AllowAnonymous();", "GET /a", "Anonymous")]
    public void MinimalApiReader_ReadsTheRouteAndItsRule(string source, string expectedKey, string expectedSecurity)
    {
        var endpoints = MinimalApis(source);

        Assert.Equal([(expectedKey, expectedSecurity)], endpoints.Select(e => (e.Key, e.Value)));
    }

    [Fact]
    public void MinimalApiReader_FailsOnAMappingItCannotRead()
    {
        Assert.Throws<InvalidOperationException>(() => MinimalApis("app.MapGet(RoutePattern, () => 1);"));
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

        foreach (var endpoint in document.RootElement.GetProperty("MinimalApis").EnumerateArray())
            rows.Add(($"Endpoint {endpoint.GetProperty("Endpoint").GetString()}", endpoint.GetProperty("ExpectedSecurity").GetString()!));

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

        var program = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, ProgramFile));
        foreach (var (key, security) in MinimalApis(program))
            result[$"Endpoint {key}"] = security;

        return result;
    }

    private static readonly Regex MapCall = new(@"\bapp\.Map(?<verb>[A-Za-z]*)\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// Reads every <c>app.Map…("route", …)</c> call and the calls chained after it, up to the end of the statement.
    /// <c>MapRazorPages</c> and <c>MapControllers</c> are covered by the page and controller rows. A mapping this
    /// can't read throws, so a new kind of mapping is noticed rather than skipped.
    /// </summary>
    private static List<KeyValuePair<string, string>> MinimalApis(string source)
    {
        var code = string.Join('\n', source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        var endpoints = new List<KeyValuePair<string, string>>();

        foreach (Match match in MapCall.Matches(code))
        {
            var verb = match.Groups["verb"].Value;
            if (verb is "RazorPages" or "Controllers")
                continue;

            var openParen = match.Index + match.Length - 1;
            var arguments = code[(openParen + 1)..ClosingParen(code, openParen)];
            var route = Regex.Match(arguments, "^\\s*\"(?<route>[^\"]*)\"");
            if (!route.Success)
                throw new InvalidOperationException($"Can't read the route of app.Map{verb}({arguments.Trim()}).");

            var chain = new List<(string Name, string Arguments)>();
            var position = ClosingParen(code, openParen) + 1;
            while (true)
            {
                var call = Regex.Match(code[position..], @"^\s*\.(?<name>[A-Za-z]+)\s*\(");
                if (!call.Success)
                    break;

                var callParen = position + call.Index + call.Length - 1;
                var close = ClosingParen(code, callParen);
                chain.Add((call.Groups["name"].Value, code[(callParen + 1)..close].Trim()));
                position = close + 1;
            }

            var method = verb.Length == 0 ? "ANY" : verb.ToUpperInvariant();
            endpoints.Add(new($"{method} {route.Groups["route"].Value}", DescribeChain(chain)));
        }

        return endpoints;
    }

    private static string DescribeChain(List<(string Name, string Arguments)> chain)
    {
        var shortCircuit = chain.Any(c => c.Name == "ShortCircuit") ? " + ShortCircuit" : "";
        if (chain.Any(c => c.Name == "AllowAnonymous"))
            return Anonymous + shortCircuit;

        var requirements = chain.Where(c => c.Name == "RequireAuthorization")
            .SelectMany(c => c.Arguments.Length == 0
                ? ["SignedIn"]
                : c.Arguments.Split(',').Select(a => $"Policy:{a.Trim().Trim('"')}"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return (requirements.Count == 0 ? "None" : string.Join(" + ", requirements)) + shortCircuit;
    }

    /// <summary>The index of the parenthesis that closes the one at <paramref name="open"/>, skipping string literals.</summary>
    private static int ClosingParen(string code, int open)
    {
        var depth = 0;
        for (var i = open; i < code.Length; i++)
        {
            switch (code[i])
            {
                case '"':
                    i++;
                    while (i < code.Length && code[i] != '"')
                        i += code[i] == '\\' ? 2 : 1;
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    if (--depth == 0)
                        return i;
                    break;
            }
        }

        throw new InvalidOperationException("Unbalanced parentheses in a minimal API mapping.");
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
    /// "Anonymous" when anything allows anonymous access (it wins over every authorization rule); otherwise
    /// every requirement, sorted and joined with " + " because they all apply; "None" when nothing protects it.
    /// A feature gate is added either way, since it still turns the page off.
    /// </summary>
    private static string Describe(IEnumerable<object> metadata)
    {
        var items = metadata.ToList();
        var gates = items.OfType<FeatureGateAttribute>().Select(DescribeGate).Distinct(StringComparer.Ordinal).ToList();

        if (items.OfType<IAllowAnonymous>().Any())
            return string.Join(" + ", [Anonymous, .. gates.Order(StringComparer.Ordinal)]);

        var requirements = items.OfType<IAuthorizeData>()
            .SelectMany(Requirements)
            .Concat(gates)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return requirements.Count == 0 ? "None" : string.Join(" + ", requirements);
    }

    private static string DescribeGate(FeatureGateAttribute gate)
    {
        var features = gate.Features.ToList();
        var list = features.Count == 1 ? features[0] : $"{gate.RequirementType}({string.Join(",", features)})";
        return gate.Negate ? $"FeatureGate:Not({list})" : $"FeatureGate:{list}";
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
