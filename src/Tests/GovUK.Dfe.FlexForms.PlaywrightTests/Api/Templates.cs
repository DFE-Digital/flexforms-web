using System.Text;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api;

public static class Templates
{
    public static Task<TemplateDto> CreateTemplateAsync(IAPIRequestContext request, string name) =>
        ApiBase.ApiRequestAsync<TemplateDto>(request, "/v1/Templates", method: "POST", data: new CreateTemplateRequest(name));

    public static Task<TemplateSchemaDto> CreateTemplateVersionAsync(
        IAPIRequestContext request,
        string templateId,
        string versionNumber,
        string schemaJson) =>
        ApiBase.ApiRequestAsync<TemplateSchemaDto>(request, $"/v1/Templates/{templateId}/versions", method: "POST",
            data: new CreateTemplateVersionRequest(versionNumber, ApiBase.EncodeJson(schemaJson)));

    /// <summary>The latest version's template JSON. Stored schemas may be base64-encoded.</summary>
    public static async Task<string> GetLatestSchemaJsonAsync(IAPIRequestContext request, string templateId)
    {
        var schema = await ApiBase.ApiRequestAsync<TemplateSchemaDto>(request, $"/v1/Templates/{templateId}/schema");
        var text = schema.JsonSchema.Trim();
        return text.StartsWith('{') ? text : Encoding.UTF8.GetString(Convert.FromBase64String(text));
    }

    public static async Task<TemplateDto?> FindTemplateByNameAsync(IAPIRequestContext request, string name) =>
        (await GetTemplatesAsync(request)).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    public static Task<IReadOnlyList<TemplateDto>> GetTemplatesAsync(IAPIRequestContext request) =>
        ApiBase.ApiRequestAsync<IReadOnlyList<TemplateDto>>(request, "/v1/Templates");

    public static Task<TemplateDto> UpdateTemplateAsync(IAPIRequestContext request, string templateId, bool live) =>
        ApiBase.ApiRequestAsync<TemplateDto>(request, $"/v1/Templates/{templateId}/live", method: "PUT",
            data: new { isLive = live });

    public static async Task<TemplateDto> UpdateTemplateLiveAsync(IAPIRequestContext request, string name, bool live)
    {
        var templateId = await GetTemplateIdByNameAsync(request, name);

        return await UpdateTemplateAsync(request, templateId, live);
    }

    public static async Task<string> GetTemplateIdByNameAsync(IAPIRequestContext request, string name)
    {
        var templates = await GetTemplatesAsync(request);

        var template = templates.FirstOrDefault(t =>
                           string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                       ?? throw new ApiRequestException($"Template '{name}' not found.");

        return template.TemplateId;
    }

    public static Task<IReadOnlyList<CustomApplicationStatus>> GetTemplateCustomStatusesAsync(
        IAPIRequestContext request, string templateId) =>
        ApiBase.ApiRequestAsync<IReadOnlyList<CustomApplicationStatus>>(request,
            $"/v1/Templates/{templateId}/custom-statuses");

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
}