using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class NotificationsPage(IPage page) : BasePage(page)
{
    public async Task GoToAsync() => await Page.GotoAsync("/notifications");

    public async Task ExpectNotificationAsync(string message) =>
        await Assertions.Expect(NotificationRow(message)).ToBeVisibleAsync();

    public async Task ExpectNotificationNotVisibleAsync(string message) =>
        await Assertions.Expect(NotificationRow(message)).Not.ToBeVisibleAsync();

    public async Task ExpectNoNotificationsAsync() =>
        await Assertions.Expect(Page.Locator("#notifications-tbody .govuk-table__row")).ToHaveCountAsync(0);

    public async Task DismissNotificationAsync(string message)
    {
        var notificationRow = NotificationRow(message);
        await notificationRow
            .GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Dismiss  this notification" })
            .ClickAsync();
    }

    public async Task ClearAllNotificationsAsync()
    {
        await ById("btn-clear-all").ClickAsync();
    }

    private ILocator NotificationRow(string message) =>
        Page.Locator("#notifications-tbody .govuk-table__row").Filter(new LocatorFilterOptions { HasText = message });
}