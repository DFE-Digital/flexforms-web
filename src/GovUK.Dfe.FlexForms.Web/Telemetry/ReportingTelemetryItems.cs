using System.Security.Cryptography;
using System.Text;
using GovUK.Dfe.FlexForms.Web.Security;

namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Request values for <see cref="ReportingTelemetryInitializer"/>, stashed on <see cref="HttpContext.Items"/>
/// by middleware that already has them. Request telemetry is tracked after the request's service scope
/// is disposed, and must never read the session (Redis), so the initializer only reads these items.
/// </summary>
internal static class ReportingTelemetryItems
{
    private const string TenantIdItemKey = "FlexForms.Telemetry.TenantId";
    private const string SessionIdItemKey = "FlexForms.Telemetry.SessionId";
    private const string SessionPurpose = "flexforms-web/session/v1";
    private const int SessionIdByteCount = 16;

    public static void BindTenant(HttpContext context, Guid tenantId)
    {
        if (tenantId != Guid.Empty)
            context.Items[TenantIdItemKey] = tenantId;
    }

    public static Guid? GetTenantId(HttpContext context) =>
        context.Items.TryGetValue(TenantIdItemKey, out var value) && value is Guid tenantId ? tenantId : null;

    /// <summary>
    /// Derives an opaque session correlation id from the ASP.NET session's internal id (not the cookie
    /// value) and the sign-in start time kept by <see cref="UserActivityTracker"/>, which is cleared on
    /// idle, absolute and explicit sign-out. Call only once the session is already loaded.
    /// </summary>
    public static void BindSession(HttpContext context, ISession session)
    {
        if (!session.IsAvailable || string.IsNullOrEmpty(session.Id))
            return;

        var startTime = session.GetString(UserActivityTracker.SessionStartKey);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{SessionPurpose}|{session.Id}|{startTime}"));
        context.Items[SessionIdItemKey] = Convert.ToHexStringLower(hash.AsSpan(0, SessionIdByteCount));
    }

    public static string? GetSessionId(HttpContext context) =>
        context.Items.TryGetValue(SessionIdItemKey, out var value) ? value as string : null;
}
