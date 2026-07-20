using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <inheritdoc />
public sealed class TemplateSelectionService(
    ITemplatesClient templatesClient,
    ILogger<TemplateSelectionService> logger) : ITemplateSelectionService
{
    private const string TemplateIdSessionKey = "TemplateId";
    private const string TemplateNameSessionKey = "TemplateName";
    private const string TemplateIsLiveSessionKey = "TemplateIsLive";
    private static readonly string[] ApplicationSessionKeysToClear =
    [
        "ApplicationId",
        "ApplicationReference",
        "FormData",
        "CurrentTaskId",
        "CurrentPageId"
    ];

    /// <inheritdoc />
    public async Task<IReadOnlyList<TemplateDto>> GetSelectableTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var templates = await templatesClient.GetAccessibleTemplatesAsync(cancellationToken);
        return templates
            .OrderByDescending(t => t.IsLive)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public string? GetSelectedTemplateId(HttpContext httpContext)
        => httpContext.Session.GetString(TemplateIdSessionKey);

    /// <inheritdoc />
    public void SelectTemplate(HttpContext httpContext, TemplateDto template)
    {
        var previous = httpContext.Session.GetString(TemplateIdSessionKey);
        var next = template.TemplateId.ToString();

        if (!string.Equals(previous, next, StringComparison.OrdinalIgnoreCase))
        {
            ClearApplicationSessionState(httpContext.Session);
        }

        httpContext.Session.SetString(TemplateIdSessionKey, next);
        httpContext.Session.SetString(TemplateNameSessionKey, template.Name);
        httpContext.Session.SetString(TemplateIsLiveSessionKey, template.IsLive.ToString());
        logger.LogInformation(
            "Selected template {TemplateId} ({TemplateName}, IsLive={IsLive}) for session",
            next,
            template.Name,
            template.IsLive);
    }

    /// <inheritdoc />
    public bool HasValidSelection(HttpContext httpContext, IReadOnlyList<TemplateDto> templates)
    {
        var selected = GetSelectedTemplateId(httpContext);
        if (string.IsNullOrWhiteSpace(selected) || !Guid.TryParse(selected, out var selectedId))
        {
            return false;
        }

        return templates.Any(t => t.TemplateId == selectedId);
    }

    /// <inheritdoc />
    public bool IsPreviewSelection(HttpContext httpContext)
        => bool.TryParse(
               httpContext.Session.GetString(TemplateIsLiveSessionKey),
               out var isLive) &&
           !isLive;

    /// <inheritdoc />
    public string? GetSelectedTemplateName(HttpContext httpContext)
        => httpContext.Session.GetString(TemplateNameSessionKey);

    private static void ClearApplicationSessionState(ISession session)
    {
        foreach (var key in ApplicationSessionKeysToClear)
        {
            session.Remove(key);
        }

        foreach (var key in session.Keys.Where(k =>
                     k.StartsWith("ApplicationStatus_", StringComparison.OrdinalIgnoreCase) ||
                     k.StartsWith("FormAccumulation_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            session.Remove(key);
        }
    }
}
