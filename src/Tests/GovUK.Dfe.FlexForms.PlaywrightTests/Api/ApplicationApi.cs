using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api;

public static class ApplicationApi
{
    public static Task<CreateApplicationResponse> CreateApplicationAsync(IAPIRequestContext request,
        CreateApplicationRequest body) =>
        ApiBase.ApiRequestAsync<CreateApplicationResponse>(request, "/v1/applications", method: "POST", data: body);

    public static async Task<CreateApplicationResponse> CreateApplicationInStatusAsync(
        IAPIRequestContext request,
        CreateApplicationRequest body,
        ApplicationStatus status)
    {
        var application = await CreateApplicationAsync(request, body);

        switch (status)
        {
            case ApplicationStatus.Created:
                break;
            case ApplicationStatus.InProgress:
                await AddApplicationResponseAsync(
                    request,
                    application.ApplicationId,
                    """{"schoolName":"Playwright status test"}""");
                break;
            case ApplicationStatus.Submitted:
                await AddApplicationResponseAsync(
                    request,
                    application.ApplicationId,
                    """{"schoolName":"Playwright status test"}""");
                await SubmitApplicationAsync(request, application.ApplicationId);
                break;
            case ApplicationStatus.Deleted:
                await DeleteApplicationAsync(request, application.ApplicationId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return application;
    }

    public static Task<CreateApplicationResponse> GetApplicationByRefAsync(IAPIRequestContext request,
        string applicationReference) =>
        ApiBase.ApiRequestAsync<CreateApplicationResponse>(request,
            $"/v1/applications/reference/{applicationReference}");

    public static Task<IReadOnlyList<UploadDto>> GetFilesAsync(IAPIRequestContext request, string applicationId) =>
        ApiBase.ApiRequestAsync<IReadOnlyList<UploadDto>>(request, $"/v1/applications/{applicationId}/files");

    public static Task AddApplicationResponseAsync(
        IAPIRequestContext request,
        string applicationId,
        string responseBodyJson) =>
        ApiBase.ApiRequestAsync<object>(
            request,
            $"/v1/applications/{applicationId}/responses",
            method: "POST",
            data: new AddApplicationResponseRequest(ApiBase.EncodeJson(responseBodyJson)));

    public static Task<CreateApplicationResponse> SubmitApplicationAsync(IAPIRequestContext request,
        string applicationId) =>
        ApiBase.ApiRequestAsync<CreateApplicationResponse>(
            request,
            $"/v1/applications/{applicationId}/submit",
            method: "POST");

    public static Task DeleteApplicationAsync(IAPIRequestContext request, string applicationId) =>
        ApiBase.ApiRequestAsync<object>(
            request,
            $"/v1/applications/{applicationId}",
            method: "DELETE");

    public static Task<UserDto> AddContributorToApplicationAsync(IAPIRequestContext request, string applicationId,
        string email) =>
        ApiBase.ApiRequestAsync<UserDto>(
            request,
            $"/v1/applications/{applicationId}/contributors",
            method: "POST",
            data: new AddContributorRequest(email, "Test Name"));
}