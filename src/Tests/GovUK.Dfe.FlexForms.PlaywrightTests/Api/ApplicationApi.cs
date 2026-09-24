using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api;

public static class ApplicationApi
{
    public static Task<CreateApplicationResponse> CreateApplicationAsync(IAPIRequestContext request, CreateApplicationRequest body) =>
        ApiBase.ApiRequestAsync<CreateApplicationResponse>(request, "/v1/applications", method: "POST", data: body);

    public static Task<CreateApplicationResponse> GetApplicationByRefAsync(IAPIRequestContext request, string applicationReference) =>
        ApiBase.ApiRequestAsync<CreateApplicationResponse>(request, $"/v1/applications/reference/{applicationReference}");

    public static Task<IReadOnlyList<UploadDto>> GetFilesAsync(IAPIRequestContext request, string applicationId) =>
        ApiBase.ApiRequestAsync<IReadOnlyList<UploadDto>>(request, $"/v1/applications/{applicationId}/files");
}
