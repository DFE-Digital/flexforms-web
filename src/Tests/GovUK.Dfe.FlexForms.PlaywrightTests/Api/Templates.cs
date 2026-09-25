using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api;

public static class Templates
{
    public static Task<IReadOnlyList<CustomApplicationStatus>> GetTemplateCustomStatusesAsync(IAPIRequestContext request, string templateId) =>
        ApiBase.ApiRequestAsync<IReadOnlyList<CustomApplicationStatus>>(request, $"/v1/Templates/{templateId}/custom-statuses");
}
