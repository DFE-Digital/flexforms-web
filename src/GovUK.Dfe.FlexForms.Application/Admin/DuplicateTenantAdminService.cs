using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Validation;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Clones the current tenant's TenantConfig into a new tenant.
/// </summary>
public interface IDuplicateTenantAdmin
{
    Task LoadInternalServiceAuthServicesAsync(
        DuplicateTenantWorkState state,
        CancellationToken cancellationToken = default);

    IReadOnlyList<FormValidationError> ValidateInput(DuplicateTenantWorkState state);

    Task<AdminPageOutcome> CloneAsync(
        DuplicateTenantWorkState state,
        CancellationToken cancellationToken = default);
}

public sealed class DuplicateTenantAdminService(
    ITenantAdminClient tenantAdminClient,
    ILogger<DuplicateTenantAdminService> logger) : IDuplicateTenantAdmin
{
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string GenerateSecretKey(int byteLength = 48) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength));

    public static string ToBase64Utf8(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

    public async Task LoadInternalServiceAuthServicesAsync(
        DuplicateTenantWorkState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await tenantAdminClient.GetTenantSettingsAsync(state.SourceTenantId, cancellationToken);
            var template = response.Settings
                .Where(s => string.Equals(s.Category, "InternalServiceAuth", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => string.Equals(s.Target, "Api", StringComparison.OrdinalIgnoreCase))
                .ThenBy(s => s.Target, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (template is null || string.IsNullOrWhiteSpace(template.SettingsJson))
            {
                state.InternalServiceAuthServiceApiKeys = [];
                return;
            }

            state.InternalServiceAuthServiceApiKeys = ParseServiceEmails(template.SettingsJson)
                .Select(email => new DuplicateTenantServiceSecret
                {
                    Email = email,
                    ApiKey = GenerateSecretKey()
                })
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not load InternalServiceAuth services for tenant {TenantId}. Service ApiKey fields will be empty.",
                state.SourceTenantId);
            state.InternalServiceAuthServiceApiKeys = [];
        }
    }

    public IReadOnlyList<FormValidationError> ValidateInput(DuplicateTenantWorkState state)
    {
        var errors = new List<FormValidationError>();
        for (var i = 0; i < state.InternalServiceAuthServiceApiKeys.Count; i++)
        {
            var service = state.InternalServiceAuthServiceApiKeys[i];
            if (string.IsNullOrWhiteSpace(service.Email))
            {
                errors.Add(new FormValidationError(
                    $"InternalServiceAuthServiceApiKeys[{i}].Email",
                    DuplicateTenantMessages.ServiceEmailRequired));
            }

            if (string.IsNullOrWhiteSpace(service.ApiKey) || service.ApiKey.Length < 32)
            {
                errors.Add(new FormValidationError(
                    $"InternalServiceAuthServiceApiKeys[{i}].ApiKey",
                    DuplicateTenantMessages.ServiceApiKeyRequired));
            }
        }

        if (state.NewTenantId == Guid.Empty)
        {
            errors.Add(new FormValidationError("NewTenantId", DuplicateTenantMessages.TenantIdRequired));
        }
        else if (state.NewTenantId == state.SourceTenantId)
        {
            errors.Add(new FormValidationError("NewTenantId", DuplicateTenantMessages.TenantIdMustDiffer));
        }

        return errors;
    }

    public async Task<AdminPageOutcome> CloneAsync(
        DuplicateTenantWorkState state,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateInput(state);
        if (errors.Count > 0)
            return AdminPageOutcome.Stay(errors: errors);

        try
        {
            // WAF-safe: hostname, frontendOrigin, serviceName, and secrets live only inside Base64 payloadJson
            // so Application Gateway does not see cleartext https:// ARGS (rule 931130 RFI).
            var secretsPayload = new CloneTenantSecretsPayload
            {
                Hostname = state.Hostname,
                FrontendOrigin = state.FrontendOrigin,
                AuthorizationApiSecretKey = state.AuthorizationApiSecretKey,
                InternalServiceAuthSecretKey = state.InternalServiceAuthSecretKey,
                InternalServiceAuthServiceApiKeys = state.InternalServiceAuthServiceApiKeys
                    .Select(s => new CloneTenantServiceApiKeyPayload
                    {
                        Email = s.Email,
                        ApiKey = s.ApiKey
                    })
                    .ToList()
            };

            var payloadNode = JsonSerializer.SerializeToNode(secretsPayload, PayloadSerializerOptions)!.AsObject();
            payloadNode["serviceName"] = state.ServiceName;

            var body = new CloneTenantRequest(
                state.NewTenantId,
                state.NewTenantName,
                ToBase64Utf8(payloadNode.ToJsonString(PayloadSerializerOptions)));

            var response = await tenantAdminClient.CloneTenantAsync(state.SourceTenantId, body, cancellationToken);

            return AdminPageOutcome.Redirect(
                successMessage: DuplicateTenantMessages.Created(
                    response.NewTenantName,
                    response.NewTenantId,
                    response.SettingsCopied,
                    response.Hostname));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to duplicate tenant {SourceTenantId} to {NewTenantId}",
                state.SourceTenantId,
                state.NewTenantId);
            var message = FormatCloneError(ex);
            state.HasError = true;
            state.ErrorMessage = message;
            return AdminPageOutcome.Stay(errorMessage: message);
        }
    }

    internal static IReadOnlyList<string> ParseServiceEmails(string settingsJson)
    {
        try
        {
            if (JsonNode.Parse(settingsJson) is not JsonObject root ||
                root["Services"] is not JsonArray services)
            {
                return [];
            }

            return services
                .OfType<JsonObject>()
                .Select(s => s["Email"]?.GetValue<string>()?.Trim() ?? string.Empty)
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string FormatCloneError(Exception ex)
    {
        if (ex is ExternalApplicationsException clientEx)
        {
            var body = clientEx.Response?.TrimStart() ?? string.Empty;
            if (clientEx.StatusCode == 403 && body.StartsWith('<'))
                return DuplicateTenantMessages.CloneBlocked;

            if (clientEx.StatusCode > 0)
                return DuplicateTenantMessages.CloneFailedHttp(clientEx.StatusCode);
        }

        return AdminApiErrorMapper.Format(ex, DuplicateTenantMessages.CloneFailed, includeGatewayHint: true);
    }
}
