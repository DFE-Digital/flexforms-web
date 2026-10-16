using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class NotificationsPage(IPage page) : BasePage(page)
{
    public async Task GoToAsync() => await Page.GotoAsync("/notifications");

    public async Task ExpectUnreadBadgeAsync(int expectedCount)
    {
        var badge = ById("notifications-unread-badge");
        await Assertions.Expect(badge).ToHaveTextAsync(expectedCount.ToString());
        await Assertions.Expect(badge).ToBeVisibleAsync();
    }

    public async Task ExpectUnreadBadgeNotVisibleAsync() =>
        await Assertions.Expect(ById("notifications-unread-badge")).ToBeHiddenAsync(
            new LocatorAssertionsToBeHiddenOptions { Timeout = 15_000 });

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

    public async Task MarkAllNotificationsAsReadAsync()
    {
        await ById("btn-mark-all-read").ClickAsync();
    }

    private ILocator NotificationRow(string message) =>
        Page.Locator("#notifications-tbody .govuk-table__row").Filter(new LocatorFilterOptions { HasText = message });
}