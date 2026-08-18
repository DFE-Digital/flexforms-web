using System.ComponentModel.DataAnnotations;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// SuperAdmin form to clone the current tenant's TenantConfig into a new tenant.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManagePlatformTenantsPolicy)]
public sealed class DuplicateTenantModel(
    IDuplicateTenantAdmin duplicateTenantAdmin,
    ITenantRequestContext tenantRequestContext) : PageModel
{
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
    [Required(ErrorMessage = "Enter a service name")]
    [StringLength(200, ErrorMessage = "Service name must be 200 characters or fewer")]
    [Display(Name = "Service name")]
    public string ServiceName { get; set; } = string.Empty;

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
    public string AuthorizationApiSecretKey { get; set; } = DuplicateTenantAdminService.GenerateSecretKey();

    [BindProperty]
    [Required(ErrorMessage = "Enter an InternalServiceAuth secret key")]
    [MinLength(32, ErrorMessage = "InternalServiceAuth secret key must be at least 32 characters")]
    [Display(Name = "InternalServiceAuth SecretKey (API and Web)")]
    public string InternalServiceAuthSecretKey { get; set; } = DuplicateTenantAdminService.GenerateSecretKey();

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

        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            ServiceName = tenantRequestContext.TenantConfiguration?["Layout:ServiceName"]
                ?? SourceTenantName
                ?? string.Empty;
        }

        var state = CaptureWorkState();
        await duplicateTenantAdmin.LoadInternalServiceAuthServicesAsync(state, cancellationToken);
        ApplyWorkState(state);
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
        {
            var state = CaptureWorkState();
            await duplicateTenantAdmin.LoadInternalServiceAuthServicesAsync(state, cancellationToken);
            ApplyWorkState(state);
        }

        AuthorizationApiSecretKey = DuplicateTenantAdminService.GenerateSecretKey();
        InternalServiceAuthSecretKey = DuplicateTenantAdminService.GenerateSecretKey();
        foreach (var service in InternalServiceAuthServiceApiKeys)
            service.ApiKey = DuplicateTenantAdminService.GenerateSecretKey();

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
        ServiceName = ServiceName?.Trim() ?? string.Empty;
        Hostname = Hostname?.Trim() ?? string.Empty;
        FrontendOrigin = FrontendOrigin?.Trim() ?? string.Empty;
        AuthorizationApiSecretKey = AuthorizationApiSecretKey?.Trim() ?? string.Empty;
        InternalServiceAuthSecretKey = InternalServiceAuthSecretKey?.Trim() ?? string.Empty;

        for (var i = 0; i < InternalServiceAuthServiceApiKeys.Count; i++)
        {
            var service = InternalServiceAuthServiceApiKeys[i];
            service.Email = service.Email?.Trim() ?? string.Empty;
            service.ApiKey = service.ApiKey?.Trim() ?? string.Empty;
        }

        var state = CaptureWorkState();
        foreach (var validationError in duplicateTenantAdmin.ValidateInput(state))
            ModelState.AddModelError(validationError.FieldKey, validationError.Message);

        if (!ModelState.IsValid)
            return Page();

        var outcome = await duplicateTenantAdmin.CloneAsync(state, cancellationToken);
        ApplyWorkState(state);

        foreach (var validationError in outcome.Errors)
            ModelState.AddModelError(validationError.FieldKey, validationError.Message);

        if (outcome.Kind == AdminPageOutcomeKind.StayOnPage)
        {
            if (outcome.ErrorMessage != null)
            {
                HasError = true;
                ErrorMessage = outcome.ErrorMessage;
            }

            return Page();
        }

        TempData["AdminSuccess"] = outcome.SuccessMessage;
        return RedirectToPage("/Admin/Admin");
    }

    private void EnsureSecretsPopulated()
    {
        if (string.IsNullOrWhiteSpace(AuthorizationApiSecretKey))
            AuthorizationApiSecretKey = DuplicateTenantAdminService.GenerateSecretKey();
        if (string.IsNullOrWhiteSpace(InternalServiceAuthSecretKey))
            InternalServiceAuthSecretKey = DuplicateTenantAdminService.GenerateSecretKey();

        foreach (var service in InternalServiceAuthServiceApiKeys)
        {
            if (string.IsNullOrWhiteSpace(service.ApiKey))
                service.ApiKey = DuplicateTenantAdminService.GenerateSecretKey();
        }
    }

    private DuplicateTenantWorkState CaptureWorkState() =>
        new()
        {
            SourceTenantId = SourceTenantId,
            SourceTenantName = SourceTenantName,
            NewTenantId = NewTenantId,
            NewTenantName = NewTenantName,
            ServiceName = ServiceName,
            Hostname = Hostname,
            FrontendOrigin = FrontendOrigin,
            AuthorizationApiSecretKey = AuthorizationApiSecretKey,
            InternalServiceAuthSecretKey = InternalServiceAuthSecretKey,
            InternalServiceAuthServiceApiKeys = InternalServiceAuthServiceApiKeys
                .Select(s => new DuplicateTenantServiceSecret
                {
                    Email = s.Email,
                    ApiKey = s.ApiKey
                })
                .ToList()
        };

    private void ApplyWorkState(DuplicateTenantWorkState state)
    {
        SourceTenantId = state.SourceTenantId;
        SourceTenantName = state.SourceTenantName;
        InternalServiceAuthServiceApiKeys = state.InternalServiceAuthServiceApiKeys
            .Select(s => new InternalServiceAuthServiceSecretInput
            {
                Email = s.Email,
                ApiKey = s.ApiKey
            })
            .ToList();

        if (state.HasError)
        {
            HasError = true;
            ErrorMessage = state.ErrorMessage;
        }
    }

    private bool TryResolveSourceTenant(out string? error)
    {
        if (tenantRequestContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            error = DuplicateTenantMessages.TenantContextMissing;
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
