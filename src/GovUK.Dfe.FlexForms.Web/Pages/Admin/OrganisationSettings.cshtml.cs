using System.ComponentModel.DataAnnotations;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Tenant Admin editor for non-secret organisation settings (terminology, banner, dashboard, check-your-answers).
/// </summary>
[Authorize(Roles = AdminAccessHelper.AuthorizeRoles)]
public sealed class OrganisationSettingsModel(
    IOrganisationSettingsAdmin organisationSettingsAdmin,
    ITenantRequestContext tenantRequestContext,
    ITenantConfigurationCache tenantConfigurationCache,
    ITenantIdResolver tenantIdResolver) : PageModel
{
    public Guid TenantId { get; private set; }

    public string TenantName { get; private set; } = string.Empty;

    public bool HasError { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool ShowSuccess { get; private set; }

    public string? SuccessMessage { get; private set; }

    [BindProperty]
    [Required(ErrorMessage = "Enter the singular term")]
    [StringLength(100)]
    public string TerminologySingular { get; set; } = "application";

    [BindProperty]
    [Required(ErrorMessage = "Enter the plural term")]
    [StringLength(100)]
    public string TerminologyPlural { get; set; } = "applications";

    [BindProperty]
    public bool BannerEnabled { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? BannerHeading { get; set; } = "Important";

    [BindProperty]
    [StringLength(2000)]
    public string? BannerMessage { get; set; } = string.Empty;

    [BindProperty]
    [Range(1, 500, ErrorMessage = "Page size must be between 1 and 500")]
    public int DashboardPageSize { get; set; } = 50;

    [BindProperty]
    public bool DashboardEnableFilters { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? DashboardMainHeading { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? DashboardInProgressHeading { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? DashboardStartNewHeading { get; set; }

    [BindProperty]
    [StringLength(500)]
    public string? DashboardStartNewHint { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? DashboardStartNewButtonText { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? PreviewPageHeading { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? PreviewSubmitHeading { get; set; }

    [BindProperty]
    [StringLength(1000)]
    public string? PreviewSubmitHint { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? PreviewSubmitButtonText { get; set; }

    [BindProperty]
    public bool PreviewHideSubmitSection { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        ApplyTempData();
        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            return Page();
        }

        var state = CaptureWorkState();
        await organisationSettingsAdmin.LoadAsync(state, cancellationToken);
        ApplyWorkState(state);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            return Page();
        }

        if (!ModelState.IsValid)
            return Page();

        TerminologySingular = TerminologySingular?.Trim() ?? string.Empty;
        TerminologyPlural = TerminologyPlural?.Trim() ?? string.Empty;
        BannerHeading = BannerHeading?.Trim() ?? "Important";
        BannerMessage = BannerMessage?.Trim() ?? string.Empty;
        DashboardMainHeading = DashboardMainHeading?.Trim() ?? string.Empty;
        DashboardInProgressHeading = DashboardInProgressHeading?.Trim() ?? string.Empty;
        DashboardStartNewHeading = DashboardStartNewHeading?.Trim() ?? string.Empty;
        DashboardStartNewHint = DashboardStartNewHint?.Trim() ?? string.Empty;
        DashboardStartNewButtonText = DashboardStartNewButtonText?.Trim() ?? string.Empty;
        PreviewPageHeading = PreviewPageHeading?.Trim() ?? string.Empty;
        PreviewSubmitHeading = PreviewSubmitHeading?.Trim() ?? string.Empty;
        PreviewSubmitHint = PreviewSubmitHint?.Trim() ?? string.Empty;
        PreviewSubmitButtonText = PreviewSubmitButtonText?.Trim() ?? string.Empty;

        var outcome = await organisationSettingsAdmin.SaveAsync(CaptureWorkState(), cancellationToken);
        return MapOutcome(outcome);
    }

    private IActionResult MapOutcome(AdminPageOutcome outcome)
    {
        if (outcome.RefreshLocalCaches)
        {
            tenantConfigurationCache.Invalidate(TenantId);
            tenantIdResolver.InvalidateHostnameCache();
        }

        if (outcome.SuccessMessage != null)
            TempData["OrganisationSettingsSuccess"] = outcome.SuccessMessage;

        if (outcome.Kind == AdminPageOutcomeKind.StayOnPage)
        {
            if (outcome.ErrorMessage != null)
            {
                HasError = true;
                ErrorMessage = outcome.ErrorMessage;
            }

            return Page();
        }

        return RedirectToPage();
    }

    private OrganisationSettingsWorkState CaptureWorkState() =>
        new()
        {
            TenantId = TenantId,
            TenantName = TenantName,
            TerminologySingular = TerminologySingular,
            TerminologyPlural = TerminologyPlural,
            BannerEnabled = BannerEnabled,
            BannerHeading = BannerHeading,
            BannerMessage = BannerMessage,
            DashboardPageSize = DashboardPageSize,
            DashboardEnableFilters = DashboardEnableFilters,
            DashboardMainHeading = DashboardMainHeading,
            DashboardInProgressHeading = DashboardInProgressHeading,
            DashboardStartNewHeading = DashboardStartNewHeading,
            DashboardStartNewHint = DashboardStartNewHint,
            DashboardStartNewButtonText = DashboardStartNewButtonText,
            PreviewPageHeading = PreviewPageHeading,
            PreviewSubmitHeading = PreviewSubmitHeading,
            PreviewSubmitHint = PreviewSubmitHint,
            PreviewSubmitButtonText = PreviewSubmitButtonText,
            PreviewHideSubmitSection = PreviewHideSubmitSection
        };

    private void ApplyWorkState(OrganisationSettingsWorkState state)
    {
        TenantId = state.TenantId;
        TenantName = state.TenantName;
        TerminologySingular = state.TerminologySingular;
        TerminologyPlural = state.TerminologyPlural;
        BannerEnabled = state.BannerEnabled;
        BannerHeading = state.BannerHeading;
        BannerMessage = state.BannerMessage;
        DashboardPageSize = state.DashboardPageSize;
        DashboardEnableFilters = state.DashboardEnableFilters;
        DashboardMainHeading = state.DashboardMainHeading;
        DashboardInProgressHeading = state.DashboardInProgressHeading;
        DashboardStartNewHeading = state.DashboardStartNewHeading;
        DashboardStartNewHint = state.DashboardStartNewHint;
        DashboardStartNewButtonText = state.DashboardStartNewButtonText;
        PreviewPageHeading = state.PreviewPageHeading;
        PreviewSubmitHeading = state.PreviewSubmitHeading;
        PreviewSubmitHint = state.PreviewSubmitHint;
        PreviewSubmitButtonText = state.PreviewSubmitButtonText;
        PreviewHideSubmitSection = state.PreviewHideSubmitSection;
        if (state.HasError)
        {
            HasError = true;
            ErrorMessage = state.ErrorMessage;
        }
    }

    private bool TryResolveTenant(out string? error)
    {
        if (tenantRequestContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            error = OrganisationSettingsMessages.TenantContextMissing;
            return false;
        }

        TenantId = tenantId;
        TenantName = tenantRequestContext.TenantName ?? string.Empty;
        error = null;
        return true;
    }

    private void ApplyTempData()
    {
        if (TempData["OrganisationSettingsSuccess"] is string success)
        {
            ShowSuccess = true;
            SuccessMessage = success;
        }

        if (TempData["OrganisationSettingsError"] is string error)
        {
            HasError = true;
            ErrorMessage = error;
        }
    }
}
