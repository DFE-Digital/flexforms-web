using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Components;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests;

[TestFixture(Description = "File upload")]
public sealed class FileUploadTests : PlaywrightTestBase
{
    private const string SupportingDocuments = "supportingDocuments";

    private CreateApplicationResponse _application = null!;
    private UploadField _uploadField = null!;

    [SetUp]
    public async Task OpenTheUploadPageOnANewApplicationAsync()
    {
        _application = await ApplicationApi.CreateApplicationAsync(
            ApiClient, ApplicationBuilder.CreateApplicationRequest(ApiConfig.TemplateId));
        _uploadField = new UploadField(Page, SupportingDocuments);

        await LoginAsync();
        await GoToUploadPageAsync();
    }

    [TestCase(TestName = "A file that fails validation shows the validator's message and stops the application being submitted until it is deleted")]
    [CiRetry]
    public async Task AFileThatFailsValidationBlocksSubmitUntilDeletedAsync()
    {
        var preview = new ApplicationPreviewPage(Page);
        var fileName = UploadField.UniqueFileName("fails-validation");

        await _uploadField.UploadAsync(fileName);
        await _uploadField.ExpectFileListedAsync(fileName);
        await Files.RecordValidationResultAsync(AdminApiClient, _application.ApplicationId, fileName,
            isValid: false, FileValidationMessage.Markdown);

        await Page.ReloadAsync();
        await _uploadField.ExpectValidationStatusAsync(fileName, "Validation failed");
        await FileValidationMessage.ExpectFormattedAsync(_uploadField.ValidationStatus(fileName));

        await preview.GoToAsync(_application.ApplicationReference);
        await preview.ExpectFileValidationBlocksSubmitAsync(fileName);
        await FileValidationMessage.ExpectFormattedAsync(preview.BlockingFile(fileName));

        await GoToUploadPageAsync();
        await _uploadField.DeleteAsync(fileName);
        await _uploadField.ExpectFileCountAsync(0);

        await preview.GoToAsync(_application.ApplicationReference);
        await preview.ExpectFileValidationDoesNotBlockSubmitAsync();
    }

    [OneTimeTearDown]
    public async Task ClearTheDefaultUsersNotificationsAsync() =>
        await Notifications.ClearAllNotificationsAsync(ApiClient);

    private async Task GoToUploadPageAsync() =>
        await new ConfiguredFieldsTask(Page).GoToAsync(_application.ApplicationReference, ConfiguredFieldsTask.UploadPage);
}
