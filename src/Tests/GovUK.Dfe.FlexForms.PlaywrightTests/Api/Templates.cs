using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api;

public static class Templates
{
    public static Task<IReadOnlyList<TemplateDto>> GetTemplatesAsync(IAPIRequestContext request) =>
        ApiBase.ApiRequestAsync<IReadOnlyList<TemplateDto>>(request, "/v1/Templates");

    public static Task<TemplateDto> UpdateTemplateAsync(IAPIRequestContext request, string templateId, bool live) =>
        ApiBase.ApiRequestAsync<TemplateDto>(request, $"/v1/Templates/{templateId}/live", method: "PUT", data: new { isLive = live });

    public static async Task<TemplateDto> UpdateTemplateLiveAsync(IAPIRequestContext request, string name, bool live)
    {
        var templates = await GetTemplatesAsync(request);

        var template = templates.FirstOrDefault(t =>
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ApiRequestException($"Template '{name}' not found.");

        return await UpdateTemplateAsync(request, template.TemplateId, live);
    }

    public static Task<IReadOnlyList<CustomApplicationStatus>> GetTemplateCustomStatusesAsync(IAPIRequestContext request, string templateId) =>
        ApiBase.ApiRequestAsync<IReadOnlyList<CustomApplicationStatus>>(request, $"/v1/Templates/{templateId}/custom-statuses");

    public static Task<CustomApplicationStatus> CreateCustomApplicationStatusAsync(
        IAPIRequestContext request,
        string templateId,
        ApplicationStatus status,
        string label) =>
        ApiBase.ApiRequestAsync<CustomApplicationStatus>(
            request,
            $"/v1/Templates/{templateId}/custom-statuses",
            method: "POST",
            data: new CustomApplicationStatusRequest(status, label));

    public static async Task<string?> GetTemplateNameAsync(IAPIRequestContext request, string templateId)
    {
        var templates = await GetTemplatesAsync(request);
        return templates.FirstOrDefault(t => t.TemplateId == templateId)?.Name;
    }
}
