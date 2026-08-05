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

        try
        {
            var templates = await templateSelectionService.GetSelectableTemplatesAsync(context.RequestAborted);
            var liveTemplates = templates.Where(template => template.IsLive).ToList();
            var selectedId = templateSelectionService.GetSelectedTemplateId(context);
            var selectedTemplate = Guid.TryParse(selectedId, out var parsedSelectedId)
                ? templates.FirstOrDefault(template => template.TemplateId == parsedSelectedId)
                : null;

            var isExplicitAdminPreview =
                GovUK.Dfe.FlexForms.Web.Security.AdminAccessHelper.CanManageTemplates(context.User) &&
                selectedTemplate is { IsLive: false } &&
                templateSelectionService.IsPreviewSelection(context);

            if (isExplicitAdminPreview)
            {
                await templateSelectionService.SelectTemplateAsync(
                    context,
                    selectedTemplate!,
                    context.RequestAborted);
                await next(context);
                return;
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
                    return;
                }

                await next(context);
                return;
            }

            if (liveTemplates.Count > 1 &&
                selectedTemplate is { IsLive: true } &&
                !IsRoot(context.Request.Path))
            {
                await templateSelectionService.SelectTemplateAsync(
                    context,
                    selectedTemplate,
                    context.RequestAborted);
                await next(context);
                return;
            }

            // 0 or many live templates — send the user to the live-template chooser.
            var returnUrl = IsRoot(context.Request.Path)
                ? DashboardPath.Value!
                : context.Request.Path + context.Request.QueryString;
            var target =
                $"{TemplatesPath.Value}?liveOnly=true&returnUrl={Uri.EscapeDataString(returnUrl)}";

            context.Response.Redirect(target);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Template selection gate failed; redirecting to template chooser");
            context.Response.Redirect(TemplatesPath.Value!);
        }
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
