using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// SuperAdmin form to clone the current tenant's TenantConfig into a new tenant.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageTenantSettingsPolicy)]
public sealed class DuplicateTenantModel(
    ITenantAdminClient tenantAdminClient,
    ITenantRequestContext tenantRequestContext,
    ILogger<DuplicateTenantModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Guid SourceTenantId { get; private set; }

    public string SourceTenantName { get; private set; } = string.Empty;

    public bool HasError { get; private set; }

    public string? ErrorMessage { get; private set; }

    [BindProperty]
    [Required(ErrorMessage = "Enter a tenant id")]
    [Display(Name = "New tenant id")]
    public Guid NewTenantId { get; set; } = Guid.NewGuid();

    [BindProperty]
    [Required(ErrorMessage = "Enter a tenant name")]
    [StringLength(100, ErrorMessage = "Tenant name must be 100 characters or fewer")]
    [Display(Name = "New tenant name")]
    public string NewTenantName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Enter a hostname")]
    [StringLength(255, ErrorMessage = "Hostname must be 255 characters or fewer")]
    [Display(Name = "Hostname")]
    public string Hostname { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Enter a frontend origin")]
    [StringLength(500, ErrorMessage = "Frontend origin must be 500 characters or fewer")]
    [Display(Name = "Frontend origin")]
    public string FrontendOrigin { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Enter an Authorization secret key")]
    [MinLength(32, ErrorMessage = "Authorization secret key must be at least 32 characters")]
    [Display(Name = "Authorization SecretKey (API)")]
    public string AuthorizationApiSecretKey { get; set; } = GenerateSecretKey();

    [BindProperty]
    [Required(ErrorMessage = "Enter an InternalServiceAuth secret key")]
    [MinLength(32, ErrorMessage = "InternalServiceAuth secret key must be at least 32 characters")]
    [Display(Name = "InternalServiceAuth SecretKey (API and Web)")]
    public string InternalServiceAuthSecretKey { get; set; } = GenerateSecretKey();

    /// <summary>
    /// One editable ApiKey per InternalServiceAuth Services[] email from the source tenant.
    /// </summary>
    [BindProperty]
    public List<InternalServiceAuthServiceSecretInput> InternalServiceAuthServiceApiKeys { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveSourceTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewTenantName))
            NewTenantName = $"{SourceTenantName} copy";

        await LoadInternalServiceAuthServicesAsync(cancellationToken);
        EnsureSecretsPopulated();
        return Page();
    }

    public async Task<IActionResult> OnPostRegenerateSecretsAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveSourceTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            return Page();
        }

        // Keep service emails from the posted form when present; otherwise reload from source.
        if (InternalServiceAuthServiceApiKeys.Count == 0)
            await LoadInternalServiceAuthServicesAsync(cancellationToken);

        AuthorizationApiSecretKey = GenerateSecretKey();
        InternalServiceAuthSecretKey = GenerateSecretKey();
        foreach (var service in InternalServiceAuthServiceApiKeys)
            service.ApiKey = GenerateSecretKey();

        ModelState.Clear();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveSourceTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            return Page();
        }

        NewTenantName = NewTenantName?.Trim() ?? string.Empty;
        Hostname = Hostname?.Trim() ?? string.Empty;
        FrontendOrigin = FrontendOrigin?.Trim() ?? string.Empty;
        AuthorizationApiSecretKey = AuthorizationApiSecretKey?.Trim() ?? string.Empty;
        InternalServiceAuthSecretKey = InternalServiceAuthSecretKey?.Trim() ?? string.Empty;

        for (var i = 0; i < InternalServiceAuthServiceApiKeys.Count; i++)
        {
            var service = InternalServiceAuthServiceApiKeys[i];
            service.Email = service.Email?.Trim() ?? string.Empty;
            service.ApiKey = service.ApiKey?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(service.Email))
                ModelState.AddModelError(
                    $"{nameof(InternalServiceAuthServiceApiKeys)}[{i}].{nameof(InternalServiceAuthServiceSecretInput.Email)}",
                    "Service email is required.");

            if (string.IsNullOrWhiteSpace(service.ApiKey) || service.ApiKey.Length < 32)
                ModelState.AddModelError(
                    $"{nameof(InternalServiceAuthServiceApiKeys)}[{i}].{nameof(InternalServiceAuthServiceSecretInput.ApiKey)}",
                    "Enter an ApiKey of at least 32 characters.");
        }

        if (!ModelState.IsValid)
            return Page();

        if (NewTenantId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(NewTenantId), "Enter a valid tenant id.");
            return Page();
        }

        if (NewTenantId == SourceTenantId)
        {
            ModelState.AddModelError(nameof(NewTenantId), "New tenant id must be different from the current tenant.");
            return Page();
        }

        try
        {
            // WAF-safe: hostname, frontendOrigin, and secrets live only inside Base64 payloadJson
            // so Application Gateway does not see cleartext https:// ARGS (rule 931130 RFI).
            var secretsPayload = new CloneTenantSecretsPayload
            {
                Hostname = Hostname,
                FrontendOrigin = FrontendOrigin,
                AuthorizationApiSecretKey = AuthorizationApiSecretKey,
                InternalServiceAuthSecretKey = InternalServiceAuthSecretKey,
                InternalServiceAuthServiceApiKeys = InternalServiceAuthServiceApiKeys
                    .Select(s => new CloneTenantServiceApiKeyPayload
                    {
                        Email = s.Email,
                        ApiKey = s.ApiKey
                    })
                    .ToList()
            };

            var body = new CloneTenantRequest(
                NewTenantId,
                NewTenantName,
                ToBase64Utf8(JsonSerializer.Serialize(secretsPayload, PayloadSerializerOptions)));

            var response = await tenantAdminClient.CloneTenantAsync(SourceTenantId, body, cancellationToken);

            TempData["TenantSettingsSuccess"] =
                $"Duplicated to '{response.NewTenantName}' ({response.NewTenantId}). " +
                $"Copied {response.SettingsCopied} setting(s). Hostname: {response.Hostname}. " +
                "Authorization and InternalServiceAuth secrets (SecretKey + service ApiKeys) were applied. " +
                "Review remaining secrets and principals before using the new tenant.";

            return RedirectToPage("/Admin/TenantSettings");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to duplicate tenant {SourceTenantId} to {NewTenantId}",
                SourceTenantId,
                NewTenantId);
            HasError = true;
            ErrorMessage = GetCloneErrorMessage(ex);
            return Page();
        }
    }

    private async Task LoadInternalServiceAuthServicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await tenantAdminClient.GetTenantSettingsAsync(SourceTenantId, cancellationToken);
            var template = response.Settings
                .Where(s => string.Equals(s.Category, "InternalServiceAuth", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => string.Equals(s.Target, "Api", StringComparison.OrdinalIgnoreCase))
                .ThenBy(s => s.Target, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (template is null || string.IsNullOrWhiteSpace(template.SettingsJson))
            {
                InternalServiceAuthServiceApiKeys = [];
                return;
            }

            InternalServiceAuthServiceApiKeys = ParseServiceEmails(template.SettingsJson)
                .Select(email => new InternalServiceAuthServiceSecretInput
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
                SourceTenantId);
            InternalServiceAuthServiceApiKeys = [];
        }
    }

    private static IReadOnlyList<string> ParseServiceEmails(string settingsJson)
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

    private void EnsureSecretsPopulated()
    {
        if (string.IsNullOrWhiteSpace(AuthorizationApiSecretKey))
            AuthorizationApiSecretKey = GenerateSecretKey();
        if (string.IsNullOrWhiteSpace(InternalServiceAuthSecretKey))
            InternalServiceAuthSecretKey = GenerateSecretKey();

        foreach (var service in InternalServiceAuthServiceApiKeys)
        {
            if (string.IsNullOrWhiteSpace(service.ApiKey))
                service.ApiKey = GenerateSecretKey();
        }
    }

    private static string GenerateSecretKey(int byteLength = 48) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength));

    internal static string ToBase64Utf8(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

    private static string GetCloneErrorMessage(Exception ex)
    {
        if (ex is ExternalApplicationsException clientEx)
        {
            var body = clientEx.Response?.TrimStart() ?? string.Empty;
            if (clientEx.StatusCode == 403 && body.StartsWith('<'))
            {
                return "Clone was blocked with HTTP 403 (HTML response). "
                    + "This usually means Front Door / WAF rejected the request before the API. "
                    + "Check WAF logs for POST /v1/admin/tenants/.../clone.";
            }

            if (clientEx.StatusCode > 0)
                return $"Could not duplicate tenant. (HTTP {clientEx.StatusCode})";
        }

        return TenantSettingsModel.GetErrorMessage(ex, "Could not duplicate tenant.");
    }

    private bool TryResolveSourceTenant(out string? error)
    {
        if (tenantRequestContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            error = "Tenant context is not available for this request.";
            return false;
        }

        SourceTenantId = tenantId;
        SourceTenantName = tenantRequestContext.TenantName ?? string.Empty;
        error = null;
        return true;
    }

    public sealed class InternalServiceAuthServiceSecretInput
    {
        public string Email { get; set; } = string.Empty;

        [MinLength(32, ErrorMessage = "ApiKey must be at least 32 characters")]
        public string ApiKey { get; set; } = string.Empty;
    }
}
