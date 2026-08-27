using System.ComponentModel.DataAnnotations;
using GovUK.Dfe.FlexForms.Domain.Templates;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Creates a draft template owned by the current tenant.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageTemplatesPolicy)]
public sealed class CreateTemplateModel(
    ITemplatesClient templatesClient,
    ITemplateSelectionService templateSelectionService,
    IUsersClient usersClient,
    IMemoryCache memoryCache,
    ILogger<CreateTemplateModel> logger) : PageModel
{
    /// <summary>
    /// Gets or sets the template display name.
    /// </summary>
    [BindProperty]
    [Required(ErrorMessage = "Enter a template name")]
    [StringLength(100, ErrorMessage = "Template name must be 100 characters or fewer")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Creates and selects the new draft template with a starter schema version.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var name = Name.Trim();

            // Create the template shell first so the starter schema can use the real template id.
            var template = await templatesClient.CreateTemplateAsync(
                new CreateTemplateRequest(name),
                cancellationToken);

            var starterSchema = StarterFormTemplateSchema.CreateBase64Json(
                template.TemplateId.ToString(),
                name);

            await templatesClient.CreateTemplateVersionAsync(
                template.TemplateId,
                new CreateTemplateVersionRequest(
                    VersionNumber: StarterFormTemplateSchema.DefaultVersionNumber,
                    JsonSchema: starterSchema),
                cancellationToken);

            // Refresh DTO version info used by template selection / nav.
            template.LatestVersionNumber = StarterFormTemplateSchema.DefaultVersionNumber;
            await templateSelectionService.SelectTemplateAsync(HttpContext, template, cancellationToken);

            // CreateTemplate grants the admin template permission and invalidates API caches.
            // Refresh the Web permissions claims so subsequent admin actions stay authorized.
            await UserPermissionsCache.RefreshAsync(
                memoryCache,
                usersClient,
                User,
                logger,
                cancellationToken,
                forceRefresh: true,
                tenantId: HttpContext.RequestServices.GetService<ITenantRequestContext>()?.TenantId);

            return RedirectToPage(
                "/Admin/TemplateManager",
                new { showForm = true, created = true, suggestedVersion = "1.0.1" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create tenant template {TemplateName}", Name);
            ModelState.AddModelError(string.Empty, "The template could not be created. Try again.");
            return Page();
        }
    }
}
