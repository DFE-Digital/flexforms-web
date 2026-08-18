using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Maps API client exceptions to the Admin UI copy previously inlined on PageModels.
/// </summary>
public static class AdminApiErrorMapper
{
    public const string GatewayBlockedMessage =
        "Save was blocked with HTTP 403 (HTML response). "
        + "This usually means an Azure gateway/WAF rejected the request before the API. "
        + "Check Front Door / App Gateway logs for /v1/admin/tenants/.../settings.";

    /// <summary>
    /// Returns the API message when present, otherwise a status-aware fallback.
    /// </summary>
    /// <param name="includeGatewayHint">
    /// When true, HTML 403 responses use the gateway/WAF copy (Tenant Settings).
    /// Event Mappings omits that hint.
    /// </param>
    public static string Format(Exception ex, string fallback, bool includeGatewayHint = false)
    {
        if (ex is ExternalApplicationsException<ExceptionResponse> apiEx
            && !string.IsNullOrWhiteSpace(apiEx.Result?.Message))
        {
            return apiEx.Result.Message;
        }

        if (ex is ExternalApplicationsException clientEx)
        {
            var body = clientEx.Response?.TrimStart() ?? string.Empty;
            if (includeGatewayHint && clientEx.StatusCode == 403 && body.StartsWith('<'))
                return GatewayBlockedMessage;

            if (clientEx.StatusCode > 0)
                return $"{fallback} (HTTP {clientEx.StatusCode})";
        }

        return fallback;
    }
}
