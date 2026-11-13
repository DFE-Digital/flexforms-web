using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Components;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService;

/// <summary>
/// The live update of an upload's validation status, sent over SignalR. The page asks Web to render the
/// Markdown message and inserts the result, so this is a different path from the one a reload takes,
/// which <see cref="FileUploadTests"/> covers.
/// </summary>
[TestFixture(Description = "File validation message")]
public sealed class FileValidationMessageTests : PlaywrightTestBase
{
    protected override bool EnableSignalR => true;

    [SetUp]
    public async Task LoginAsDefaultUserAsync() => await LoginAsync();

    [TestCase(TestName = "A file validation result appears on the upload page without a reload, with its message formatted")]
    [CiRetry]
    public async Task AValidationResultAppearsWithoutAReloadAsync()
    {
        var application = await ApplicationApi.CreateApplicationAsync(
            ApiClient, ApplicationBuilder.CreateApplicationRequest(ApiConfig.TemplateId));
        var uploadField = new UploadField(Page, "supportingDocuments");
        var fileName = UploadField.UniqueFileName("live-validation");

        await new ConfiguredFieldsTask(Page).GoToAsync(application.ApplicationReference, ConfiguredFieldsTask.UploadPage);
        await uploadField.UploadAsync(fileName);
        await uploadField.ExpectFileListedAsync(fileName);

        await Assertions.Expect(Page.Locator(".notification-item").Filter(new LocatorFilterOptions
            {
                HasText = $"Your file '{fileName}' uploaded.",
            }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });

        await Files.RecordValidationResultAsync(AdminApiClient, application.ApplicationId, fileName,
            isValid: false, FileValidationMessage.Markdown);

        await uploadField.ExpectValidationStatusAsync(fileName, "Validation failed");
        await FileValidationMessage.ExpectFormattedAsync(uploadField.ValidationStatus(fileName));
    }
}
