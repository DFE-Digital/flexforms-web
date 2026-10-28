using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services;

namespace GovUK.Dfe.FlexForms.Web.Middleware;

/// <summary>
/// Ensures an authenticated user has a valid live template before entering application routes.
/// Admins may also enter an explicitly selected non-live template as a preview.
/// </summary>
public sealed class TemplateSelectionMiddleware(
    RequestDelegate next,
    ILogger<TemplateSelectionMiddleware> logger)
{
    private static readonly PathString TemplatesPath = new("/templates");
    private static readonly PathString DashboardPath = new("/applications/dashboard");

    public async Task InvokeAsync(HttpContext context, ITemplateSelectionService templateSelectionService)
    {
        if (!ShouldEnforce(context))
        {
            await next(context);
            return;
        }

        bool proceed;
        try
        {
            proceed = await ApplyGateAsync(context, templateSelectionService);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Template selection gate failed; redirecting to template chooser");
            context.Response.Redirect(TemplatesPath.Value!);
            return;
        }

        // Outside the try: failures in the page itself must surface as errors, not be reported as
        // a gate failure and bounced back to the chooser.
        if (proceed)
        {
            await next(context);
        }
    }

    /// <summary>
    /// Resolves the template selection for the request. Returns <c>true</c> when the request should
    /// continue down the pipeline, or <c>false</c> when a redirect has been issued.
    /// </summary>
    private async Task<bool> ApplyGateAsync(HttpContext context, ITemplateSelectionService templateSelectionService)
    {
        var templates = await templateSelectionService.GetSelectableTemplatesAsync(context.RequestAborted);

        if (await TrySelectTemplateFromQueryAsync(context, templates, templateSelectionService, context.RequestAborted))
        {
            return true;
        }

        var liveTemplates = templates.Where(template => template.IsLive).ToList();
        var selectedId = templateSelectionService.GetSelectedTemplateId(context);
        var selectedTemplate = Guid.TryParse(selectedId, out var parsedSelectedId)
            ? templates.FirstOrDefault(template => template.TemplateId == parsedSelectedId)
            : null;

        var isExplicitAdminPreview =
            AdminAccessHelper.CanManageTemplates(context.User) &&
            selectedTemplate is { IsLive: false } &&
            templateSelectionService.IsPreviewSelection(context);

        if (isExplicitAdminPreview)
        {
            await templateSelectionService.SelectTemplateAsync(
                context,
                selectedTemplate!,
                context.RequestAborted);
            return true;
        }

        if (liveTemplates.Count == 1)
        {
            await templateSelectionService.SelectTemplateAsync(
                context,
                liveTemplates[0],
                context.RequestAborted);
            logger.LogDebug(
                "Auto-selected sole live template {TemplateId}",
                liveTemplates[0].TemplateId);

            if (IsRoot(context.Request.Path))
            {
                context.Response.Redirect(DashboardPath.Value!);
                return false;
            }

            return true;
        }

        if (templates.Count == 0)
        {
            // Freshly cloned / empty tenants: admins must create a template first.
            if (AdminAccessHelper.CanManageTemplates(context.User))
            {
                context.Response.Redirect("/admin/create-template");
                return false;
            }

            var emptyReturnUrl = IsRoot(context.Request.Path)
                ? DashboardPath.Value!
                : context.Request.Path + context.Request.QueryString;
            context.Response.Redirect(
                $"{TemplatesPath.Value}?liveOnly=true&returnUrl={Uri.EscapeDataString(emptyReturnUrl)}");
            return false;
        }

        if (liveTemplates.Count > 1 &&
            selectedTemplate is { IsLive: true } &&
            !IsRoot(context.Request.Path))
        {
            await templateSelectionService.SelectTemplateAsync(
                context,
                selectedTemplate,
                context.RequestAborted);
            return true;
        }

        // 0 live (drafts only) or many live templates — send to the chooser.
        var returnUrl = IsRoot(context.Request.Path)
            ? DashboardPath.Value!
            : context.Request.Path + context.Request.QueryString;
        var target =
            $"{TemplatesPath.Value}?liveOnly=true&returnUrl={Uri.EscapeDataString(returnUrl)}";

        context.Response.Redirect(target);
        return false;
    }

    private static bool ShouldEnforce(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var path = context.Request.Path;
        if (AuthenticationPathExclusions.ShouldSkip(path))
        {
            return false;
        }

        if (path.StartsWithSegments(TemplatesPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.StartsWithSegments("/Error", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/lib", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Enforce for landing and application areas; admin can open /templates and /admin freely.
        return IsRoot(path) ||
               path.StartsWithSegments("/applications", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRoot(PathString path)
        => !path.HasValue || path.Value is "/" or "";

    private static async Task<bool> TrySelectTemplateFromQueryAsync(
        HttpContext context,
        IReadOnlyList<GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response.TemplateDto> templates,
        ITemplateSelectionService templateSelectionService,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Query.TryGetValue("templateId", out var templateIdValues))
            return false;

        var templateIdValue = templateIdValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(templateIdValue) || !Guid.TryParse(templateIdValue, out var templateId))
            return false;

        var template = templates.FirstOrDefault(t => t.TemplateId == templateId);
        if (template is null)
            return false;

        await templateSelectionService.SelectTemplateAsync(context, template, cancellationToken);
        return true;
    }
}

/// <summary>
/// Extension methods for <see cref="TemplateSelectionMiddleware"/>.
/// </summary>
public static class TemplateSelectionMiddlewareExtensions
{
    /// <summary>
    /// Adds template selection gating after authentication.
    /// </summary>
    public static IApplicationBuilder UseTemplateSelection(this IApplicationBuilder app)
        => app.UseMiddleware<TemplateSelectionMiddleware>();
}
