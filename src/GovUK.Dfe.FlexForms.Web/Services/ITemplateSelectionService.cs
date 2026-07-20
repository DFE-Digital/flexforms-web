using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Resolves and persists the active template for the current user session.
/// </summary>
public interface ITemplateSelectionService
{
    /// <summary>
    /// Returns templates the current caller may open (live for end users; all for admins).
    /// </summary>
    Task<IReadOnlyList<TemplateDto>> GetSelectableTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the template id currently stored in session, if any.
    /// </summary>
    string? GetSelectedTemplateId(HttpContext httpContext);

    /// <summary>
    /// Sets the active template and its preview metadata in session, clearing
    /// application-scoped state when the template changes.
    /// </summary>
    void SelectTemplate(HttpContext httpContext, TemplateDto template);

    /// <summary>
    /// Returns true when the session template is present in <paramref name="templates"/>.
    /// </summary>
    bool HasValidSelection(HttpContext httpContext, IReadOnlyList<TemplateDto> templates);

    /// <summary>
    /// Returns true when the selected template is a non-live Admin preview.
    /// </summary>
    bool IsPreviewSelection(HttpContext httpContext);

    /// <summary>
    /// Returns the selected template display name stored in session.
    /// </summary>
    string? GetSelectedTemplateName(HttpContext httpContext);
}
