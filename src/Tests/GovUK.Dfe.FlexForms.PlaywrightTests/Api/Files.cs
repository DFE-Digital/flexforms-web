using System.Text.RegularExpressions;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api;

public static class Files
{
    private static readonly HashSet<string> ReservedApplicationPathSegments = ["dashboard"];

    public static string ApplicationReferenceFromUrl(string url)
    {
        var pathname = new Uri(url).AbsolutePath;
        var match = Regex.Match(pathname, "^/applications/([^/]+)");
        var applicationRef = match.Success ? match.Groups[1].Value : null;

        if (string.IsNullOrEmpty(applicationRef) || ReservedApplicationPathSegments.Contains(applicationRef.ToLowerInvariant()))
        {
            throw new InvalidOperationException($"Could not read application reference from URL: {url}");
        }

        return applicationRef;
    }

    public static Task<FileValidationResult> ValidationResultAsync(IAPIRequestContext request, FileValidationRequest body, string fileId)
    {
        var fileValidationApiKey = TestEnvironment.OptionalEnvironmentVariable("FILE_VALIDATION_API_KEY");

        return ApiBase.ApiRequestAsync<FileValidationResult>(
            request,
            $"/v1/integrations/files/{fileId}/validation-result",
            method: "POST",
            data: body,
            headers: fileValidationApiKey is not null ? new Dictionary<string, string> { ["X-Api-Key"] = fileValidationApiKey } : null);
    }

    public static async Task ValidateValidFileForApplicationAsync(IAPIRequestContext request, IPage page)
    {
        var applicationRef = ApplicationReferenceFromUrl(page.Url);
        var application = await ApplicationApi.GetApplicationByRefAsync(request, applicationRef);
        var files = await ApplicationApi.GetFilesAsync(request, application.ApplicationId);
        var fileId = files.FirstOrDefault()?.Id;

        if (fileId is null)
        {
            throw new InvalidOperationException($"No files found for application {applicationRef}");
        }

        await ValidationResultAsync(request, new FileValidationRequest(true, "File is valid", null, "test-source"), fileId);
    }
}
