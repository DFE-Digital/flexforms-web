using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;
using NUnit.Framework;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService;

[TestFixture(Description = "Notifications")]
public sealed class NotificationTests : PlaywrightTestBase
{
    private CreateApplicationResponse _createdApplication = null!;

    [OneTimeSetUp]
    public async Task CreateApplicationAsync()
    {
        await Notifications.ClearAllNotificationsAsync(ApiClient);
        var createRequest = ApplicationBuilder.CreateApplicationRequest(ApiConfig.TemplateId);
        _createdApplication =
            await ApplicationApi.CreateApplicationInStatusAsync(ApiClient, createRequest,
                ApplicationStatus.Created);
    }

    [SetUp]
    public async Task LoginAsDefaultUserAsync() => await LoginAsync();

    [TestCase(TestName = "A notification is shown when a file is uploaded and when validated")]
    [CiRetry]
    public async Task UploadingAFileShowsANotificationAsync()
    {
        var configuredFieldsTask = new ConfiguredFieldsTask(Page);
        var notificationsPage = new NotificationsPage(Page);

        await configuredFieldsTask.GoToAsync(_createdApplication.ApplicationReference, ConfiguredFieldsTask.UploadPage);
        var uploadedFileName = await configuredFieldsTask.UploadSupportingDocumentAsync();

        await notificationsPage.NavigateToAsync(NavigationSection.Notifications);

        await notificationsPage.ExpectNotificationAsync($"Your file '{uploadedFileName}' uploaded.");

        await Files.ValidateValidFile(ApiClient, _createdApplication.ApplicationReference);
        await Page.ReloadAsync();

        await notificationsPage.ExpectNotificationAsync($"The file '{uploadedFileName}' has been validated.");
    }

    [TestCase(TestName = "User can dismiss a notification and clear all notifications")]
    [CiRetry]
    public async Task UserCanDismissANotificationAndClearAllNotificationsAsync()
    {
        await Notifications.AddNotificationAsync(ApiClient, "Test info", NotificationType.Info);
        await Notifications.AddNotificationAsync(ApiClient, "Test success", NotificationType.Success);
        await Notifications.AddNotificationAsync(ApiClient, "Test error", NotificationType.Error);

        var notificationsPage = new NotificationsPage(Page);
        await notificationsPage.GoToAsync();

        await notificationsPage.ExpectNotificationAsync("Test info");
        await notificationsPage.ExpectNotificationAsync("Test success");
        await notificationsPage.ExpectNotificationAsync("Test error");

        await notificationsPage.DismissNotificationAsync("Test info");
        await notificationsPage.ExpectNotificationNotVisibleAsync("Test info");
        await notificationsPage.ExpectNotificationAsync("Test success");
        await notificationsPage.ExpectNotificationAsync("Test error");

        await notificationsPage.ClearAllNotificationsAsync();
        await notificationsPage.ExpectNotificationNotVisibleAsync("Test success");
        await notificationsPage.ExpectNotificationNotVisibleAsync("Test error");
        await notificationsPage.ExpectNoNotificationsAsync();
    }
}