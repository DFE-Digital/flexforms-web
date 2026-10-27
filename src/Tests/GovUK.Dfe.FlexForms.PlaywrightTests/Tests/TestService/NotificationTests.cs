using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService;

[TestFixture(Description = "Notifications")]
public sealed class NotificationTests : PlaywrightTestBase
{
    private CreateApplicationResponse _createdApplication = null!;

    protected override bool EnableSignalR => true;

    // Admin is used in these tests because user has rate limiting on /notifications endpoints

    [OneTimeSetUp]
    public async Task CreateApplicationAsync()
    {
        var createRequest = ApplicationBuilder.CreateApplicationRequest(ApiConfig.TemplateId);
        _createdApplication =
            await ApplicationApi.CreateApplicationInStatusAsync(AdminApiClient, createRequest,
                ApplicationStatus.Created);
    }

    [SetUp]
    public async Task ClearNotificationsAndLogin()
    {
        await Notifications.ClearAllNotificationsAsync(AdminApiClient);
        await LoginAsync("admin");
    }

    [TestCase(TestName = "A notification is shown when a file is uploaded and when validated")]
    [CiRetry]
    public async Task UploadingAFileShowsANotificationAsync()
    {
        var configuredFieldsTask = new ConfiguredFieldsTask(Page);
        var notificationsPage = new NotificationsPage(Page);

        await configuredFieldsTask.GoToAsync(_createdApplication.ApplicationReference, ConfiguredFieldsTask.UploadPage);
        var uploadedFileName = await configuredFieldsTask.UploadSupportingDocumentAsync();

        await notificationsPage.ExpectUnreadBadgeAsync(1);
        await notificationsPage.NavigateToAsync(NavigationSection.Notifications);

        await notificationsPage.ExpectNotificationAsync($"Your file '{uploadedFileName}' uploaded.");

        await Files.ValidateValidFile(AdminApiClient, _createdApplication.ApplicationReference);
        await Page.ReloadAsync();

        await notificationsPage.ExpectUnreadBadgeAsync(2);

        // extend timeout for file-validation pipeline completing
        await notificationsPage.ExpectNotificationAsync(
            $"The file '{uploadedFileName}' has been validated.", timeout: 20_000);
    }

    [TestCase(TestName = "User can dismiss a notification and clear all notifications")]
    [CiRetry]
    public async Task UserCanDismissANotificationAndClearAllNotificationsAsync()
    {
        await Notifications.AddNotificationAsync(AdminApiClient, "Test info", NotificationType.Info);
        await Notifications.AddNotificationAsync(AdminApiClient, "Test success", NotificationType.Success);
        await Notifications.AddNotificationAsync(AdminApiClient, "Test error", NotificationType.Error);

        var notificationsPage = new NotificationsPage(Page);
        await notificationsPage.GoToAsync();

        await notificationsPage.ExpectUnreadBadgeAsync(3);
        await notificationsPage.ExpectNotificationAsync("Test info");
        await notificationsPage.ExpectNotificationAsync("Test success");
        await notificationsPage.ExpectNotificationAsync("Test error");

        await notificationsPage.DismissNotificationAsync("Test info");
        await notificationsPage.ExpectNotificationNotVisibleAsync("Test info");
        await notificationsPage.ExpectNotificationAsync("Test success");
        await notificationsPage.ExpectNotificationAsync("Test error");

        // bug 309578, dismiss requires a reload to update the unread badge count
        await Page.ReloadAsync();
        await notificationsPage.ExpectUnreadBadgeAsync(2);

        await notificationsPage.ClearAllNotificationsAsync();
        await notificationsPage.ExpectNotificationNotVisibleAsync("Test success");
        await notificationsPage.ExpectNotificationNotVisibleAsync("Test error");
        await notificationsPage.ExpectNoNotificationsAsync();
        
        // bug 309578, dismiss requires a reload to update the unread badge count
        await Page.ReloadAsync();
        await notificationsPage.ExpectUnreadBadgeNotVisibleAsync();
    }

    [TestCase(TestName = "User can mark all notifications as read")]
    [CiRetry]
    public async Task UserCanMarkAllNotificationsAsReadAsync()
    {
        await Notifications.AddNotificationAsync(AdminApiClient, "Test warning", NotificationType.Warning);
        await Notifications.AddNotificationAsync(AdminApiClient, "Test success", NotificationType.Success);

        var notificationsPage = new NotificationsPage(Page);
        await notificationsPage.GoToAsync();

        await notificationsPage.ExpectUnreadBadgeAsync(2);
        await notificationsPage.MarkAllNotificationsAsReadAsync();
        await notificationsPage.ExpectUnreadBadgeNotVisibleAsync();
        await notificationsPage.ExpectNotificationAsync("Test warning");
        await notificationsPage.ExpectNotificationAsync("Test success");
    }
}