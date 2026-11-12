using System.Security.Claims;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Adds pseudonymous user, session, tenant and traffic-classification fields to server telemetry for
/// usage reporting. Never overwrites a value another initializer has already set, and reads only
/// <see cref="HttpContext.User"/>, the request path/headers and <see cref="ReportingTelemetryItems"/>.
/// </summary>
public sealed class ReportingTelemetryInitializer(
    IHttpContextAccessor httpContextAccessor,
    ReportingUserIdGenerator reportingUserIdGenerator) : ITelemetryInitializer
{
    public const string ServiceNameValue = "flexforms-web";

    internal static class Keys
    {
        public const string ServiceName = "ServiceName";
        public const string TenantId = "TenantId";
        public const string SessionId = "SessionId";
        public const string ReportingUserId = "ReportingUserId";
        public const string UserType = "UserType";
        public const string IsAuthenticated = "IsAuthenticated";
        public const string IsHealthCheck = "IsHealthCheck";
        public const string IsStaticResource = "IsStaticResource";
        public const string IsSyntheticTraffic = "IsSyntheticTraffic";
    }

    private const string AvailabilityTestHeader = "SyntheticTest-RunId";

    private static readonly string[] StaticPathPrefixes =
        ["/css", "/js", "/lib", "/assets", "/images", "/fonts", "/govuk", "/_framework", "/favicon.ico"];

    private static readonly string[] StaticExtensions =
        [".js", ".css", ".map", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".woff", ".woff2", ".ttf", ".eot", ".webp"];

    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is not ISupportProperties supportProperties)
            return;

        var properties = supportProperties.Properties;
        properties.TryAdd(Keys.ServiceName, ServiceNameValue);

        var context = httpContextAccessor.HttpContext;
        if (context is null)
            return;

        var tenantId = ReportingTelemetryItems.GetTenantId(context);
        if (tenantId is Guid id)
            properties.TryAdd(Keys.TenantId, id.ToString());

        var sessionId = ReportingTelemetryItems.GetSessionId(context);
        if (sessionId is not null)
        {
            properties.TryAdd(Keys.SessionId, sessionId);
            if (string.IsNullOrEmpty(telemetry.Context.Session.Id))
                telemetry.Context.Session.Id = sessionId;
        }

        var user = context.User;
        var isAuthenticated = user.Identity?.IsAuthenticated == true;
        if (isAuthenticated)
            ApplyUser(telemetry, properties, user, tenantId);

        if (telemetry is RequestTelemetry request)
            ApplyRequestClassification(request, properties, context, isAuthenticated);
    }

    private void ApplyUser(ITelemetry telemetry, IDictionary<string, string> properties, ClaimsPrincipal user, Guid? tenantId)
    {
        var reportingUserId = tenantId is Guid id
            ? reportingUserIdGenerator.Create(id, user.FindFirstValue(ClaimTypes.NameIdentifier))
            : null;

        if (reportingUserId is not null)
        {
            properties.TryAdd(Keys.ReportingUserId, reportingUserId);
            if (string.IsNullOrEmpty(telemetry.Context.User.AuthenticatedUserId))
                telemetry.Context.User.AuthenticatedUserId = reportingUserId;
        }

        var userType = GetUserType(user);
        if (userType is not null)
            properties.TryAdd(Keys.UserType, userType);
    }

    private static void ApplyRequestClassification(
        RequestTelemetry request,
        IDictionary<string, string> properties,
        HttpContext context,
        bool isAuthenticated)
    {
        var path = context.Request.Path;
        properties.TryAdd(Keys.IsAuthenticated, ToFlag(isAuthenticated));
        properties.TryAdd(Keys.IsHealthCheck, ToFlag(HealthCheckTelemetryFilter.IsHealthCheckPath(path.Value)));
        properties.TryAdd(Keys.IsStaticResource, ToFlag(IsStaticResource(path)));

        // Only ever "true": absence means unknown, not "a person".
        if (!string.IsNullOrEmpty(request.Context.Operation.SyntheticSource)
            || context.Request.Headers.ContainsKey(AvailabilityTestHeader))
        {
            properties.TryAdd(Keys.IsSyntheticTraffic, ToFlag(true));
        }
    }

    /// <summary>Existing roles and the internal service-account claim only; no new classification.</summary>
    private static string? GetUserType(ClaimsPrincipal user)
    {
        if (user.HasClaim("service_type", "internal"))
            return "InternalService";
        if (AdminAccessHelper.IsSuperAdmin(user))
            return "SuperAdmin";
        if (user.IsInRole("Admin"))
            return "Admin";
        if (user.IsInRole("User"))
            return "User";
        return null;
    }

    private static bool IsStaticResource(PathString path)
    {
        if (!path.HasValue)
            return false;

        return StaticPathPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            || StaticExtensions.Any(extension => path.Value!.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToFlag(bool value) => value ? "true" : "false";
}
