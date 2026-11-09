using System.ComponentModel.DataAnnotations;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// SuperAdmin form to create a new tenant from the minimum settings, prefilled from the current tenant.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManagePlatformTenantsPolicy)]
public sealed class NewTenantModel(
    INewTenantAdmin newTenantAdmin,
    ITenantRequestContext tenantRequestContext,
    IHostEnvironment hostEnvironment) : PageModel
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
    [Required(ErrorMessage = NewTenantMessages.ServiceNameRequired)]
    [StringLength(NewTenantAdminService.ServiceNameMaxLength, ErrorMessage = NewTenantMessages.ServiceNameTooLong)]
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
    public string Scheme { get; set; } = string.Empty;

    [BindProperty]
    public List<SettingInput> Settings { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveSourceTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewTenantName))
            NewTenantName = $"{SourceTenantName} new";

        var state = CaptureWorkState();
        await newTenantAdmin.LoadDraftAsync(state, cancellationToken);
        ApplyWorkState(state);
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

        var state = CaptureWorkState();
        foreach (var validationError in newTenantAdmin.ValidateInput(state))
            ModelState.AddModelError(validationError.FieldKey, validationError.Message);

        if (!ModelState.IsValid)
            return Page();

        var outcome = await newTenantAdmin.CreateAsync(state, cancellationToken);
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

    private NewTenantWorkState CaptureWorkState() =>
        new()
        {
            SourceTenantId = SourceTenantId,
            SourceTenantName = SourceTenantName,
            NewTenantId = NewTenantId,
            NewTenantName = NewTenantName,
            ServiceName = ServiceName,
            Hostname = Hostname,
            FrontendOrigin = FrontendOrigin,
            Scheme = Scheme,
            TestAuthenticationAllowed = TestAuthenticationEnvironmentGate.IsAllowed(hostEnvironment),
            Settings = Settings
                .Select(s => new NewTenantSettingDraft
                {
                    Category = s.Category,
                    Target = s.Target,
                    SettingsJson = s.SettingsJson ?? string.Empty,
                    IsOptional = s.IsOptional
                })
                .ToList()
        };

    private void ApplyWorkState(NewTenantWorkState state)
    {
        SourceTenantId = state.SourceTenantId;
        SourceTenantName = state.SourceTenantName;
        ServiceName = state.ServiceName;
        Scheme = state.Scheme;
        Settings = state.Settings
            .Select(s => new SettingInput
            {
                Category = s.Category,
                Target = s.Target,
                SettingsJson = s.SettingsJson,
                IsOptional = s.IsOptional
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
            error = NewTenantMessages.TenantContextMissing;
            return false;
        }

        SourceTenantId = tenantId;
        SourceTenantName = tenantRequestContext.TenantName ?? string.Empty;
        error = null;
        return true;
    }

    public sealed class SettingInput
    {
        public string Category { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public string? SettingsJson { get; set; }

        public bool IsOptional { get; set; }
    }
}
