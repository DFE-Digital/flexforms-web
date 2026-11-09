using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Validation;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Creates a new tenant from the minimum settings it needs to run, prefilled from the current tenant.
/// </summary>
public interface INewTenantAdmin
{
    /// <summary>
    /// Fills <see cref="NewTenantWorkState.Scheme"/> and <see cref="NewTenantWorkState.Settings"/>
    /// from the current tenant. Secret leaves stay <c>__REDACTED__</c>; the API copies them on create.
    /// </summary>
    Task LoadDraftAsync(NewTenantWorkState state, CancellationToken cancellationToken = default);

    IReadOnlyList<FormValidationError> ValidateInput(NewTenantWorkState state);

    Task<AdminPageOutcome> CreateAsync(NewTenantWorkState state, CancellationToken cancellationToken = default);
}

public sealed class NewTenantAdminService(
    ITenantAdminClient tenantAdminClient,
    ILogger<NewTenantAdminService> logger) : INewTenantAdmin
{
    public const string SharedTarget = "Shared";
    public const string ApiTarget = "Api";
    public const string WebTarget = "Web";

    public const string DfESignIn = "DfESignIn";
    public const string EntraSso = "EntraSso";
    public const string TestAuthentication = "TestAuthentication";

    /// <summary>
    /// Not needed to run but usually configured before go-live, so they are copied as-is when the
    /// current tenant has them (no placeholders). Must stay within the API's
    /// <c>NewTenantSettingsRequirements.Categories</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> CopiedCategories =
    [
        "FeatureManagement",
        "FileStorage",
        "Template",
        "NotificationService",
        "NotificationBanner",
        "MassTransit",
        "FrontendSettings",
        "FormEngine",
        "ExternalApplicationsApiClient",
        EntraSso,
        DfESignIn,
        "Email",
        "CacheSettings",
        "ApplicationTerminology",
        "ApplicationTemplates",
        "ApplicationReference"
    ];

    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    public static string GenerateSecretKey(int byteLength = 48) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength));

    public static string ToBase64Utf8(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

    public async Task LoadDraftAsync(NewTenantWorkState state, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SourceSetting> source;
        try
        {
            var response = await tenantAdminClient.GetTenantSettingsAsync(state.SourceTenantId, cancellationToken);
            source = response.Settings
                .Select(s => new SourceSetting(s.Category, s.Target, s.SettingsJson))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load settings for tenant {TenantId} to prefill New tenant.", state.SourceTenantId);
            source = [];
            state.HasError = true;
            state.ErrorMessage = NewTenantMessages.SettingsLoadFailed;
        }

        state.Scheme = ResolveScheme(source);
        state.Settings = BuildDraft(source, state.Scheme, state.NewTenantId, state.SourceTenantName);
    }

    public IReadOnlyList<FormValidationError> ValidateInput(NewTenantWorkState state)
    {
        var errors = new List<FormValidationError>();

        if (state.NewTenantId == Guid.Empty)
            errors.Add(new FormValidationError("NewTenantId", NewTenantMessages.TenantIdRequired));
        else if (state.NewTenantId == state.SourceTenantId)
            errors.Add(new FormValidationError("NewTenantId", NewTenantMessages.TenantIdMustDiffer));

        for (var i = 0; i < state.Settings.Count; i++)
        {
            var setting = state.Settings[i];
            if (string.IsNullOrWhiteSpace(setting.SettingsJson))
                continue;

            try
            {
                using var _ = JsonDocument.Parse(setting.SettingsJson);
            }
            catch (JsonException)
            {
                errors.Add(new FormValidationError(
                    $"Settings[{i}].SettingsJson",
                    NewTenantMessages.InvalidJson(setting.Category, setting.Target)));
            }
        }

        if (state.Settings.All(s => string.IsNullOrWhiteSpace(s.SettingsJson)))
            errors.Add(new FormValidationError("Settings", NewTenantMessages.SettingsRequired));

        return errors;
    }

    public async Task<AdminPageOutcome> CreateAsync(NewTenantWorkState state, CancellationToken cancellationToken = default)
    {
        var errors = ValidateInput(state);
        if (errors.Count > 0)
            return AdminPageOutcome.Stay(errors: errors);

        try
        {
            // WAF-safe: hostname, frontendOrigin and settings live only inside Base64 payloadJson
            // so Application Gateway does not see cleartext https:// ARGS (rule 931130 RFI).
            var payload = new
            {
                state.Hostname,
                state.FrontendOrigin,
                Settings = state.Settings
                    .Where(s => !string.IsNullOrWhiteSpace(s.SettingsJson))
                    .Select(s => new { s.Category, s.Target, s.SettingsJson })
                    .ToList()
            };

            var body = new CloneTenantRequest(
                state.NewTenantId,
                state.NewTenantName,
                ToBase64Utf8(JsonSerializer.Serialize(payload, PayloadSerializerOptions)));

            var response = await tenantAdminClient.CloneTenantAsync(state.SourceTenantId, body, cancellationToken);

            return AdminPageOutcome.Redirect(
                successMessage: NewTenantMessages.Created(
                    response.NewTenantName,
                    response.NewTenantId,
                    response.SettingsCopied,
                    response.Hostname));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to create tenant {NewTenantId} from {SourceTenantId}",
                state.NewTenantId,
                state.SourceTenantId);
            var message = FormatCreateError(ex);
            state.HasError = true;
            state.ErrorMessage = message;
            return AdminPageOutcome.Stay(errorMessage: message);
        }
    }

    /// <summary>
    /// Mirrors Web's interactive scheme precedence: explicit <c>Authentication:Scheme</c>
    /// (Web row before Shared), then TestAuthentication / EntraSso <c>Enabled</c>, else DfE Sign-in.
    /// </summary>
    internal static string ResolveScheme(IReadOnlyList<SourceSetting> source)
    {
        var webRows = source
            .Where(s => IsTarget(s, WebTarget) || IsTarget(s, SharedTarget))
            .OrderByDescending(s => IsTarget(s, WebTarget))
            .ToList();

        var explicitScheme = webRows
            .Where(s => IsCategory(s, "Authentication"))
            .Select(s => ParseObject(s.SettingsJson)?["Scheme"]?.GetValue<string>())
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        if (explicitScheme is not null)
        {
            return explicitScheme.Trim().ToLowerInvariant() switch
            {
                "test" or "testauth" or "testauthentication" => TestAuthentication,
                "entra" or "entrasso" or "entra-sso" or "microsoft" => EntraSso,
                _ => DfESignIn
            };
        }

        if (webRows.Any(s => IsCategory(s, TestAuthentication) && IsEnabled(s.SettingsJson)))
            return TestAuthentication;
        if (webRows.Any(s => IsCategory(s, EntraSso) && IsEnabled(s.SettingsJson)))
            return EntraSso;
        return DfESignIn;
    }

    /// <summary>
    /// The minimum rows for <paramref name="scheme"/>; must stay in step with the API's
    /// <c>NewTenantSettingsRequirements</c>, which rejects anything else.
    /// </summary>
    internal static List<NewTenantSettingDraft> BuildDraft(
        IReadOnlyList<SourceSetting> source,
        string scheme,
        Guid newTenantId,
        string sourceTenantName)
    {
        var draft = new List<NewTenantSettingDraft>();

        void CopyOrPlaceholder(string category, string placeholderTarget, string placeholderJson)
        {
            var rows = source.Where(s => IsCategory(s, category)).ToList();
            if (rows.Count == 0)
            {
                draft.Add(Draft(category, placeholderTarget, placeholderJson));
                return;
            }

            draft.AddRange(rows.Select(s => Draft(category, s.Target, s.SettingsJson)));
        }

        CopyOrPlaceholder("ConnectionStrings", ApiTarget, """{"DefaultConnection":""}""");
        draft.Add(AuthorizationDraft(source, newTenantId));
        CopyOrPlaceholder("AzureAd", ApiTarget, """{"TenantId":"","ClientId":""}""");
        draft.Add(Draft(
            "Authentication",
            source.FirstOrDefault(s => IsCategory(s, "Authentication"))?.Target ?? WebTarget,
            new JsonObject { ["Scheme"] = scheme }.ToJsonString()));

        switch (scheme)
        {
            case EntraSso:
                CopyOrPlaceholder(EntraSso, SharedTarget, """{"Enabled":true,"TenantId":"","ClientId":""}""");
                break;

            case TestAuthentication:
                draft.AddRange(TestAuthenticationDrafts(source, newTenantId));
                CopyOrPlaceholder("Email", ApiTarget, """{"Provider":"GovUkNotify","GovUkNotify":{"ApiKey":""}}""");
                CopyOrPlaceholder(
                    "EmailTemplates",
                    ApiTarget,
                    new JsonObject { [sourceTenantName] = new JsonObject { ["TestAuthPasswordEmail"] = "" } }.ToJsonString());
                break;

            default:
                CopyOrPlaceholder(DfESignIn, SharedTarget, """{"Authority":"","DiscoveryEndpoint":"","ClientId":"","ClientSecret":""}""");
                break;
        }

        // Web builds token-refresh options on every request, from TokenRefresh or else DfESignIn.
        if (source.Any(s => IsCategory(s, "TokenRefresh")))
        {
            CopyOrPlaceholder("TokenRefresh", WebTarget, "{}");
        }
        else if (scheme != DfESignIn)
        {
            if (source.Any(s => IsCategory(s, DfESignIn)))
                CopyOrPlaceholder(DfESignIn, SharedTarget, "{}");
            else
                draft.Add(Draft(
                    "TokenRefresh",
                    WebTarget,
                    """{"TokenEndpoint":"","IntrospectionEndpoint":"","ClientId":"","ClientSecret":""}"""));
        }

        foreach (var category in CopiedCategories)
        {
            if (draft.Any(d => string.Equals(d.Category, category, StringComparison.OrdinalIgnoreCase)))
                continue;

            draft.AddRange(source
                .Where(s => IsCategory(s, category))
                .Select(s =>
                {
                    var copied = Draft(category, s.Target, s.SettingsJson);
                    copied.IsOptional = true;
                    return copied;
                }));
        }

        return draft;
    }

    private static NewTenantSettingDraft AuthorizationDraft(IReadOnlyList<SourceSetting> source, Guid newTenantId)
    {
        var existing = source.FirstOrDefault(s =>
            IsCategory(s, "Authorization") && (IsTarget(s, ApiTarget) || IsTarget(s, SharedTarget)));

        var root = ParseObject(existing?.SettingsJson) ?? new JsonObject();
        root.Remove("SecretKey");
        root.Remove("Issuer");
        root.Remove("Audience");

        if (root["TokenSettings"] is not JsonObject tokenSettings)
        {
            tokenSettings = new JsonObject { ["TokenLifetimeMinutes"] = 60 };
            root["TokenSettings"] = tokenSettings;
        }

        tokenSettings["SecretKey"] = GenerateSecretKey();
        tokenSettings["Issuer"] = newTenantId.ToString();
        tokenSettings["Audience"] = $"api-audience-{newTenantId:D}";

        return Draft("Authorization", existing?.Target ?? ApiTarget, root.ToJsonString());
    }

    private static IEnumerable<NewTenantSettingDraft> TestAuthenticationDrafts(
        IReadOnlyList<SourceSetting> source,
        Guid newTenantId)
    {
        var rows = source.Where(s => IsCategory(s, TestAuthentication)).ToList();
        var targets = rows.Any(s => IsTarget(s, SharedTarget))
            ? [SharedTarget]
            : new[] { ApiTarget, WebTarget };

        // Web mints the test JWT and the API validates it, so every row gets the same values.
        var signingKey = GenerateSecretKey();
        foreach (var target in targets)
        {
            var root = ParseObject(rows.FirstOrDefault(s => IsTarget(s, target))?.SettingsJson) ?? new JsonObject();
            root["Enabled"] = true;
            root["JwtSigningKey"] = signingKey;
            root["JwtIssuer"] = newTenantId.ToString();
            root["JwtAudience"] = $"test-audience-{newTenantId:D}";
            yield return Draft(TestAuthentication, target, root.ToJsonString());
        }
    }

    private static NewTenantSettingDraft Draft(string category, string target, string json) =>
        new() { Category = category, Target = target, SettingsJson = Indent(json) };

    private static string Indent(string json)
    {
        try
        {
            return JsonNode.Parse(json)?.ToJsonString(IndentedJson) ?? json;
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static JsonObject? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsEnabled(string json)
    {
        var enabled = ParseObject(json)?["Enabled"];
        return enabled is JsonValue value
               && ((value.TryGetValue<bool>(out var flag) && flag)
                   || (value.TryGetValue<string>(out var text) && bool.TryParse(text, out var parsed) && parsed));
    }

    private static bool IsCategory(SourceSetting setting, string category) =>
        string.Equals(setting.Category, category, StringComparison.OrdinalIgnoreCase);

    private static bool IsTarget(SourceSetting setting, string target) =>
        string.Equals(setting.Target, target, StringComparison.OrdinalIgnoreCase);

    private static string FormatCreateError(Exception ex)
    {
        if (ex is ExternalApplicationsException { StatusCode: 403 } clientEx
            && (clientEx.Response?.TrimStart() ?? string.Empty).StartsWith('<'))
        {
            return NewTenantMessages.CreateBlocked;
        }

        // Surfaces the API's validation message (which setting is missing) when it sent one.
        return AdminApiErrorMapper.Format(ex, NewTenantMessages.CreateFailed);
    }

    internal sealed record SourceSetting(string Category, string Target, string SettingsJson);
}
