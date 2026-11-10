using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using GovUK.Dfe.FlexForms.Web.Telemetry;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Keys = GovUK.Dfe.FlexForms.Web.Telemetry.ReportingTelemetryInitializer.Keys;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Telemetry;

public class ReportingUserIdGeneratorTests
{
    private const string Key = "0123456789abcdef0123456789abcdef-test-key";
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Create_ShouldBeDeterministic_ForTheSameUserAndTenant()
    {
        var first = new ReportingUserIdGenerator(Key).Create(TenantA, "dsi-user-1");
        var second = new ReportingUserIdGenerator(Key).Create(TenantA, "dsi-user-1");

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{32}$", first);
    }

    [Fact]
    public void Create_ShouldDiffer_ForDifferentUsersTenantsAndKeys()
    {
        var generator = new ReportingUserIdGenerator(Key);
        var baseline = generator.Create(TenantA, "dsi-user-1");

        Assert.NotEqual(baseline, generator.Create(TenantA, "dsi-user-2"));
        Assert.NotEqual(baseline, generator.Create(TenantB, "dsi-user-1"));
        Assert.NotEqual(baseline, new ReportingUserIdGenerator(Key + "-rotated").Create(TenantA, "dsi-user-1"));
    }

    [Fact]
    public void Create_ShouldNotContainTheRawUserId()
    {
        var id = new ReportingUserIdGenerator(Key).Create(TenantA, "ada@education.gov.uk");

        Assert.DoesNotContain("ada", id);
        Assert.DoesNotContain("education", id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too-short-key")]
    public void Create_ShouldReturnNull_WhenTheKeyIsMissingOrTooShort(string? key)
    {
        var generator = new ReportingUserIdGenerator(key);

        Assert.False(generator.IsEnabled);
        Assert.Null(generator.Create(TenantA, "dsi-user-1"));
    }

    [Fact]
    public void ForLogging_ShouldMatchTheReportingId_AndNeverReturnTheRawId()
    {
        var generator = new ReportingUserIdGenerator(Key);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "dsi-user-1")], "Cookies"))
        };

        Assert.Equal("unknown", generator.ForLogging(context));

        ReportingTelemetryItems.BindTenant(context, TenantA);
        Assert.Equal(generator.Create(TenantA, "dsi-user-1"), generator.ForLogging(context));
        Assert.Equal("unknown", new ReportingUserIdGenerator(null).ForLogging(context));
    }

    [Fact]
    public void Create_ShouldReturnNull_WithoutUserOrTenant()
    {
        var generator = new ReportingUserIdGenerator(Key);

        Assert.Null(generator.Create(TenantA, null));
        Assert.Null(generator.Create(TenantA, " "));
        Assert.Null(generator.Create(Guid.Empty, "dsi-user-1"));
    }
}

public class ReportingTelemetryItemsTests
{
    [Fact]
    public void BindSession_ShouldStoreAnOpaqueIdThatIsStableWithinASignInSession()
    {
        var session = new FakeSession("aspnet-session-id");
        session.SetString("Session:StartTime", "2026-11-10T09:00:00.0000000Z");

        var first = Bind(session);
        var second = Bind(session);

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{32}$", first);
        Assert.DoesNotContain("aspnet-session-id", first);
    }

    [Fact]
    public void BindSession_ShouldChange_WhenANewSignInSessionStarts()
    {
        var session = new FakeSession("aspnet-session-id");
        session.SetString("Session:StartTime", "2026-11-10T09:00:00.0000000Z");
        var before = Bind(session);

        session.SetString("Session:StartTime", "2026-11-10T10:00:00.0000000Z");

        Assert.NotEqual(before, Bind(session));
    }

    [Fact]
    public void BindSession_ShouldSkip_WhenTheSessionIsUnavailable()
    {
        Assert.Null(Bind(new FakeSession("aspnet-session-id", isAvailable: false)));
    }

    [Fact]
    public void BindTenant_ShouldIgnoreAnEmptyTenant()
    {
        var context = new DefaultHttpContext();

        ReportingTelemetryItems.BindTenant(context, Guid.Empty);

        Assert.Null(ReportingTelemetryItems.GetTenantId(context));
    }

    private static string? Bind(ISession session)
    {
        var context = new DefaultHttpContext();
        ReportingTelemetryItems.BindSession(context, session);
        return ReportingTelemetryItems.GetSessionId(context);
    }
}

public class ReportingTelemetryInitializerTests
{
    private const string Key = "0123456789abcdef0123456789abcdef-test-key";
    private const string RawUserId = "dsi-user-1";
    private const string RawEmail = "ada@education.gov.uk";
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ReportingTelemetryInitializer CreateInitializer(HttpContext? context, string? key = Key)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        return new ReportingTelemetryInitializer(accessor, new ReportingUserIdGenerator(key));
    }

    private static DefaultHttpContext Request(string path = "/applications/dashboard", ClaimsPrincipal? user = null, bool withTenant = true)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (user is not null)
            context.User = user;
        if (withTenant)
            ReportingTelemetryItems.BindTenant(context, TenantId);
        return context;
    }

    private static ClaimsPrincipal SignedIn(params Claim[] extraClaims) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, RawUserId),
                new Claim(ClaimTypes.Email, RawEmail),
                new Claim(ClaimTypes.Name, "Ada Lovelace"),
                .. extraClaims
            ],
            authenticationType: "Cookies"));

    [Fact]
    public void Initialize_ShouldSetPseudonymousAuthenticatedUserId_ForSignedInRequests()
    {
        var request = new RequestTelemetry();

        CreateInitializer(Request(user: SignedIn())).Initialize(request);

        var expected = new ReportingUserIdGenerator(Key).Create(TenantId, RawUserId);
        Assert.Equal(expected, request.Context.User.AuthenticatedUserId);
        Assert.Equal(expected, request.Properties[Keys.ReportingUserId]);
        Assert.Equal(TenantId.ToString(), request.Properties[Keys.TenantId]);
        Assert.Equal("true", request.Properties[Keys.IsAuthenticated]);
        Assert.Equal("flexforms-web", request.Properties[Keys.ServiceName]);
    }

    [Fact]
    public void Initialize_ShouldNeverExportRawIdentity()
    {
        var request = new RequestTelemetry();
        var dependency = new DependencyTelemetry();
        var exception = new ExceptionTelemetry(new InvalidOperationException("boom"));
        var initializer = CreateInitializer(Request(user: SignedIn(new Claim(ClaimTypes.Role, "Admin"))));

        initializer.Initialize(request);
        initializer.Initialize(dependency);
        initializer.Initialize(exception);

        foreach (var telemetry in new ITelemetry[] { request, dependency, exception })
        {
            var exported = ((ISupportProperties)telemetry).Properties.Values
                .Append(telemetry.Context.User.AuthenticatedUserId ?? string.Empty)
                .Append(telemetry.Context.User.Id ?? string.Empty)
                .Append(telemetry.Context.Session.Id ?? string.Empty);
            Assert.DoesNotContain(exported, v => v.Contains(RawUserId) || v.Contains(RawEmail) || v.Contains("Ada"));
        }

        Assert.Equal(request.Context.User.AuthenticatedUserId, dependency.Context.User.AuthenticatedUserId);
        Assert.Equal(request.Context.User.AuthenticatedUserId, exception.Context.User.AuthenticatedUserId);
    }

    [Fact]
    public void Initialize_ShouldNotSetUserIds_ForAnonymousRequests()
    {
        var request = new RequestTelemetry();

        CreateInitializer(Request()).Initialize(request);

        Assert.Null(request.Context.User.AuthenticatedUserId);
        Assert.False(request.Properties.ContainsKey(Keys.ReportingUserId));
        Assert.False(request.Properties.ContainsKey(Keys.UserType));
        Assert.Equal("false", request.Properties[Keys.IsAuthenticated]);
        Assert.Equal(TenantId.ToString(), request.Properties[Keys.TenantId]);
    }

    [Fact]
    public void Initialize_ShouldFailSafe_WhenTheKeyIsMissing()
    {
        var request = new RequestTelemetry();

        CreateInitializer(Request(user: SignedIn()), key: null).Initialize(request);

        Assert.Null(request.Context.User.AuthenticatedUserId);
        Assert.False(request.Properties.ContainsKey(Keys.ReportingUserId));
        Assert.Equal("true", request.Properties[Keys.IsAuthenticated]);
        Assert.Equal(TenantId.ToString(), request.Properties[Keys.TenantId]);
    }

    [Fact]
    public void Initialize_ShouldNotSetAUserId_WhenTheTenantIsUnknown()
    {
        var request = new RequestTelemetry();

        CreateInitializer(Request(user: SignedIn(), withTenant: false)).Initialize(request);

        Assert.Null(request.Context.User.AuthenticatedUserId);
        Assert.False(request.Properties.ContainsKey(Keys.TenantId));
    }

    [Fact]
    public void Initialize_ShouldNotOverwriteExistingValues()
    {
        var context = Request(user: SignedIn());
        ReportingTelemetryItems.BindSession(context, new FakeSession("aspnet-session-id"));
        var request = new RequestTelemetry();
        request.Context.User.Id = "ai-anonymous-user";
        request.Context.User.AuthenticatedUserId = "existing-pseudonym";
        request.Context.Session.Id = "browser-ai-session";
        request.Properties[Keys.TenantId] = "existing-tenant";

        CreateInitializer(context).Initialize(request);

        Assert.Equal("ai-anonymous-user", request.Context.User.Id);
        Assert.Equal("existing-pseudonym", request.Context.User.AuthenticatedUserId);
        Assert.Equal("browser-ai-session", request.Context.Session.Id);
        Assert.Equal("existing-tenant", request.Properties[Keys.TenantId]);
        Assert.Equal(ReportingTelemetryItems.GetSessionId(context), request.Properties[Keys.SessionId]);
    }

    [Fact]
    public void Initialize_ShouldUseTheServerSessionId_WhenNoBrowserSessionExists()
    {
        var context = Request(user: SignedIn());
        ReportingTelemetryItems.BindSession(context, new FakeSession("aspnet-session-id"));
        var request = new RequestTelemetry();

        CreateInitializer(context).Initialize(request);

        var sessionId = ReportingTelemetryItems.GetSessionId(context);
        Assert.Equal(sessionId, request.Context.Session.Id);
        Assert.Equal(sessionId, request.Properties[Keys.SessionId]);
        Assert.NotEqual("aspnet-session-id", request.Context.Session.Id);
        Assert.NotEqual(request.Properties[Keys.ReportingUserId], request.Properties[Keys.SessionId]);
    }

    [Theory]
    [InlineData("/health", true, false)]
    [InlineData("/HEALTHZ", true, false)]
    [InlineData("/css/site.css", false, true)]
    [InlineData("/assets/fonts/bold.woff2", false, true)]
    [InlineData("/govuk-frontend.min.js", false, true)]
    [InlineData("/applications/dashboard", false, false)]
    [InlineData("/jsonsomething", false, false)]
    public void Initialize_ShouldClassifyHealthChecksAndStaticResources(string path, bool isHealthCheck, bool isStatic)
    {
        var request = new RequestTelemetry();

        CreateInitializer(Request(path)).Initialize(request);

        Assert.Equal(isHealthCheck ? "true" : "false", request.Properties[Keys.IsHealthCheck]);
        Assert.Equal(isStatic ? "true" : "false", request.Properties[Keys.IsStaticResource]);
    }

    [Fact]
    public void Initialize_ShouldFlagSyntheticTraffic_OnlyWhenKnown()
    {
        var availabilityTest = new RequestTelemetry();
        availabilityTest.Context.Operation.SyntheticSource = "Application Insights Availability Monitoring";
        var headerOnly = Request();
        headerOnly.Request.Headers["SyntheticTest-RunId"] = "run-1";
        var headerRequest = new RequestTelemetry();
        var ordinary = new RequestTelemetry();

        CreateInitializer(Request()).Initialize(availabilityTest);
        CreateInitializer(headerOnly).Initialize(headerRequest);
        CreateInitializer(Request(user: SignedIn())).Initialize(ordinary);

        Assert.Equal("true", availabilityTest.Properties[Keys.IsSyntheticTraffic]);
        Assert.Equal("true", headerRequest.Properties[Keys.IsSyntheticTraffic]);
        Assert.False(ordinary.Properties.ContainsKey(Keys.IsSyntheticTraffic));
    }

    [Theory]
    [InlineData("service_type", "internal", "InternalService")]
    [InlineData(ClaimTypes.Role, "SuperAdmin", "SuperAdmin")]
    [InlineData(ClaimTypes.Role, "Admin", "Admin")]
    [InlineData(ClaimTypes.Role, "User", "User")]
    public void Initialize_ShouldUseExistingUserClassifications(string claimType, string claimValue, string expected)
    {
        var request = new RequestTelemetry();

        CreateInitializer(Request(user: SignedIn(new Claim(claimType, claimValue)))).Initialize(request);

        Assert.Equal(expected, request.Properties[Keys.UserType]);
    }

    [Fact]
    public void Initialize_ShouldOmitUserType_ForCustomRolesOnly()
    {
        var request = new RequestTelemetry();

        CreateInitializer(Request(user: SignedIn(new Claim(ClaimTypes.Role, "Caseworker")))).Initialize(request);

        Assert.False(request.Properties.ContainsKey(Keys.UserType));
    }

    [Fact]
    public void Initialize_ShouldOnlyAddServiceName_WithoutAnHttpContext()
    {
        var trace = new TraceTelemetry("background work");

        CreateInitializer(null).Initialize(trace);

        Assert.Equal("flexforms-web", Assert.Single(trace.Properties).Value);
        Assert.Null(trace.Context.User.AuthenticatedUserId);
        Assert.Null(trace.Context.Session.Id);
    }

    [Fact]
    public void Initialize_ShouldOnlyClassifyRequests()
    {
        var dependency = new DependencyTelemetry();

        CreateInitializer(Request(user: SignedIn())).Initialize(dependency);

        Assert.True(dependency.Properties.ContainsKey(Keys.ReportingUserId));
        Assert.False(dependency.Properties.ContainsKey(Keys.IsAuthenticated));
        Assert.False(dependency.Properties.ContainsKey(Keys.IsHealthCheck));
    }
}

internal sealed class FakeSession(string id, bool isAvailable = true) : ISession
{
    private readonly Dictionary<string, byte[]> _values = [];

    public bool IsAvailable => isAvailable;
    public string Id => id;
    public IEnumerable<string> Keys => _values.Keys;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => _values.TryGetValue(key, out value);
    public void Set(string key, byte[] value) => _values[key] = value;
    public void Remove(string key) => _values.Remove(key);
    public void Clear() => _values.Clear();
}
