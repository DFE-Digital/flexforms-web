using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Pages.Templates;

/// <summary>
/// Lets the user choose which form dashboard to open.
/// Admins also see non-live forms for preview.
/// </summary>
[ExcludeFromCodeCoverage]
[Authorize]
public sealed class IndexModel(
    ITemplateSelectionService templateSelectionService,
    IFormTemplateProvider formTemplateProvider,
    ILogger<IndexModel> logger) : PageModel
{
    /// <summary>Forms available to the current user, with template JSON description.</summary>
    public IReadOnlyList<TemplateChoice> Templates { get; private set; } = [];

    /// <summary>Optional return URL after selection.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// When true, only live forms are shown for post-login selection.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public bool LiveOnly { get; set; }

    /// <summary>Selected form id from the radio list.</summary>
    [BindProperty]
    public Guid? SelectedTemplateId { get; set; }

    /// <summary>True when the caller is an Admin.</summary>
    public bool IsAdmin { get; private set; }

    /// <summary>Error message when selection fails.</summary>
    public string? ErrorMessage { get; private set; }

    public sealed record TemplateChoice(TemplateDto Template, string? Description);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        IsAdmin = AdminAccessHelper.CanManageTemplates(User);
        await LoadTemplatesAsync(cancellationToken);

        if (Templates.Count == 1 && (LiveOnly || !IsAdmin))
        {
            await templateSelectionService.SelectTemplateAsync(
                HttpContext,
                Templates[0].Template,
                cancellationToken);
            return Redirect(GetSafeReturnUrl());
        }

        var currentSelection = templateSelectionService.GetSelectedTemplateId(HttpContext);
        if (Guid.TryParse(currentSelection, out var selectedId) &&
            Templates.Any(template => template.Template.TemplateId == selectedId))
        {
            SelectedTemplateId = selectedId;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSelectAsync(CancellationToken cancellationToken)
    {
        IsAdmin = AdminAccessHelper.CanManageTemplates(User);
        await LoadTemplatesAsync(cancellationToken);

        if (SelectedTemplateId is null)
        {
            ErrorMessage = "Select a form.";
            return Page();
        }

        if (Templates.All(t => t.Template.TemplateId != SelectedTemplateId.Value))
        {
            ErrorMessage = "You do not have access to that form.";
            logger.LogWarning("User attempted to select inaccessible form {TemplateId}", SelectedTemplateId);
            return Page();
        }

        var template = Templates.First(item => item.Template.TemplateId == SelectedTemplateId.Value).Template;
        await templateSelectionService.SelectTemplateAsync(HttpContext, template, cancellationToken);
        return Redirect(GetSafeReturnUrl());
    }

    private async Task LoadTemplatesAsync(CancellationToken cancellationToken)
    {
        var templates = await templateSelectionService.GetSelectableTemplatesAsync(cancellationToken);
        if (LiveOnly)
        {
            templates = templates.Where(template => template.IsLive).ToList();
        }

        Templates = await Task.WhenAll(templates.Select(async template =>
        {
            var description = await TryGetTemplateDescriptionAsync(template.TemplateId, cancellationToken);
            return new TemplateChoice(template, description);
        }));
    }

    private async Task<string?> TryGetTemplateDescriptionAsync(Guid templateId, CancellationToken cancellationToken)
    {
        try
        {
            var formTemplate = await formTemplateProvider.GetTemplateAsync(templateId.ToString(), cancellationToken);
            return string.IsNullOrWhiteSpace(formTemplate.Description) ? null : formTemplate.Description.Trim();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not load description for template {TemplateId}", templateId);
            return null;
        }
    }

    private string GetSafeReturnUrl()
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl) &&
            Url.IsLocalUrl(ReturnUrl) &&
            !ReturnUrl.StartsWith("/templates", StringComparison.OrdinalIgnoreCase))
        {
            return ReturnUrl;
        }

        return "/applications/dashboard";
    }
}
