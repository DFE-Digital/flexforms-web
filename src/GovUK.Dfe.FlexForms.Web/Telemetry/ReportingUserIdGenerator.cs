using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Turns a user's <c>NameIdentifier</c> into a stable pseudonymous reporting id for Application Insights:
/// HMAC-SHA256 keyed by host configuration, scoped to the tenant so the same person gets unrelated ids
/// in different tenants. Without a key it returns null; the raw id is never used as a fallback.
/// </summary>
public sealed class ReportingUserIdGenerator
{
    public const string ConfigurationKey = "Telemetry:ReportingUserIdKey";
    internal const int MinimumKeyLength = 32;

    private const string Purpose = "flexforms-web/reporting-user/v1";
    private const int IdByteCount = 16;

    private readonly byte[]? _key;

    public ReportingUserIdGenerator(string? key)
    {
        var trimmed = key?.Trim();
        if (!string.IsNullOrEmpty(trimmed) && trimmed.Length >= MinimumKeyLength)
            _key = Encoding.UTF8.GetBytes(trimmed);
    }

    public bool IsEnabled => _key is not null;

    public string? Create(Guid tenantId, string? userId)
    {
        if (_key is null || tenantId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
            return null;

        var message = Encoding.UTF8.GetBytes($"{Purpose}|{tenantId:D}|{userId}");
        var hash = HMACSHA256.HashData(_key, message);
        return Convert.ToHexStringLower(hash.AsSpan(0, IdByteCount));
    }

    /// <summary>
    /// The signed-in user's reporting id for log messages (logs reach Application Insights), or
    /// <c>"unknown"</c>. Never returns the raw id.
    /// </summary>
    public string ForLogging(HttpContext context) =>
        (ReportingTelemetryItems.GetTenantId(context) is Guid tenantId
            ? Create(tenantId, context.User.FindFirstValue(ClaimTypes.NameIdentifier))
            : null) ?? "unknown";
}
