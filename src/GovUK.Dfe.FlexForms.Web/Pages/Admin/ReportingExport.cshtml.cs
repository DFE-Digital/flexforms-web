using System.Diagnostics.CodeAnalysis;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Tenant Admin page for choosing which answers are exported to reporting (current tenant).
/// </summary>
[ExcludeFromCodeCoverage]
[Authorize(Policy = AdminAccessHelper.CanManageTenantSettingsPolicy)]
public sealed class ReportingExportModel(
    IReportingExportAdmin reportingExportAdmin,
    ITenantRequestContext tenantRequestContext) : PageModel
{
    private const string SuccessKey = "ReportingExportSuccess";
    private const string ErrorKey = "ReportingExportError";

    public IReadOnlyList<TemplateDto> AvailableTemplates { get; private set; } = [];

    public ReportingExportDefaultDto? TenantDefault { get; private set; }

    public ReportingExportDefaultDto? TemplateDefault { get; private set; }

    public ReportingExportPolicyDto? Policy { get; private set; }

    public string? PolicyMessage { get; private set; }

    public ReportingExportRefreshDto? Refresh { get; private set; }

    public bool HasError { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool ShowSuccess { get; private set; }

    public string? SuccessMessage { get; private set; }

    [BindProperty(SupportsGet = true)]
    public Guid? SelectedTemplateId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? RefreshId { get; set; }

    [BindProperty]
    public ReportingExportModeSetting Mode { get; set; }

    [BindProperty]
    public string? Reason { get; set; }

    [BindProperty]
    public List<string> SelectedFields { get; set; } = [];

    public static string FieldKey(ReportingExportFieldDto field) =>
        ReportingExportAdminService.FieldKey(field.ParentFieldId, field.FieldId);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        ApplyTempData();
        if (!TryCaptureWorkState(out var state))
        {
            return Page();
        }

        await reportingExportAdmin.LoadAsync(state, cancellationToken);
        ApplyWorkState(state);
        return Page();
    }

    public Task<IActionResult> OnPostTenantDefaultAsync(CancellationToken cancellationToken) =>
        DispatchAsync(state => reportingExportAdmin.SetTenantDefaultAsync(state, Mode, Reason, cancellationToken));

    public Task<IActionResult> OnPostTemplateDefaultAsync(CancellationToken cancellationToken) =>
        DispatchAsync(state => reportingExportAdmin.SetTemplateDefaultAsync(state, Mode, Reason, cancellationToken));

    public Task<IActionResult> OnPostAllowAsync(CancellationToken cancellationToken) =>
        DispatchAsync(state => reportingExportAdmin.DecideAsync(state, SelectedFields, ReportingExportDecision.Allowed, Reason, cancellationToken));

    public Task<IActionResult> OnPostDenyAsync(CancellationToken cancellationToken) =>
        DispatchAsync(state => reportingExportAdmin.DecideAsync(state, SelectedFields, ReportingExportDecision.Denied, Reason, cancellationToken));

    private async Task<IActionResult> DispatchAsync(Func<ReportingExportWorkState, Task<AdminPageOutcome>> execute)
    {
        if (!TryCaptureWorkState(out var state))
        {
            TempData[ErrorKey] = ReportingExportMessages.TenantContextMissing;
            return RedirectToPage();
        }

        var outcome = await execute(state);
        if (outcome.SuccessMessage != null)
            TempData[SuccessKey] = outcome.SuccessMessage;

        if (outcome.ErrorMessage != null)
            TempData[ErrorKey] = outcome.ErrorMessage;

        return RedirectToPage(outcome.RouteValues.ToDictionary(kv => kv.Key, kv => (object?)kv.Value));
    }

    private bool TryCaptureWorkState([NotNullWhen(true)] out ReportingExportWorkState? state)
    {
        if (tenantRequestContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            HasError = true;
            ErrorMessage = ReportingExportMessages.TenantContextMissing;
            state = null;
            return false;
        }

        state = new ReportingExportWorkState
        {
            TenantId = tenantId,
            SelectedTemplateId = SelectedTemplateId is { } id && id != Guid.Empty ? id : null,
            RefreshId = RefreshId,
        };
        return true;
    }

    private void ApplyWorkState(ReportingExportWorkState state)
    {
        AvailableTemplates = state.AvailableTemplates;
        TenantDefault = state.TenantDefault;
        TemplateDefault = state.TemplateDefault;
        Policy = state.Policy;
        PolicyMessage = state.PolicyMessage;
        Refresh = state.Refresh;
        if (state.HasError)
        {
            HasError = true;
            ErrorMessage = state.ErrorMessage;
        }
    }

    private void ApplyTempData()
    {
        if (TempData[SuccessKey] is string success)
        {
            ShowSuccess = true;
            SuccessMessage = success;
        }

        if (TempData[ErrorKey] is string error)
        {
            HasError = true;
            ErrorMessage = error;
        }
    }
}
