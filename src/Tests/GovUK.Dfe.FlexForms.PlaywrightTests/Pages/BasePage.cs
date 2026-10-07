using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public enum NavigationSection
{
    Home,
    Applications,
    Forms,
    Admin,
    Notifications,
    LogOut
}

public abstract class BasePage(IPage page)
{
    protected IPage Page { get; } = page;

    protected ILocator ById(string id) => Page.Locator($"[id=\"{id}\"]");

    protected async Task AcceptDialogAsync(Func<Task> action)
    {
        var dialogAccepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async void AcceptDialog(object? _, IDialog dialog)
        {
            try
            {
                await dialog.AcceptAsync();
                dialogAccepted.SetResult();
            }
            catch (Exception exception)
            {
                dialogAccepted.SetException(exception);
            }
        }

        Page.Dialog += AcceptDialog;
        try
        {
            await action();
            await dialogAccepted.Task;
        }
        finally
        {
            Page.Dialog -= AcceptDialog;
        }
    }

    public async Task GoToAsync(NavigationSection section)
    {
        var sectionName = section switch
        {
            NavigationSection.Home => "Test Automation Service",
            NavigationSection.LogOut => "Log out",
            _ => section.ToString()
        };

        await ById("navigation")
            .GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = sectionName })
            .ClickAsync();
    }

    public async Task ExpectHeading(string heading) => await Assertions
        .Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = heading })).ToBeVisibleAsync();

    public async Task ExpectText(string paragraph) =>
        await Assertions.Expect(Page.GetByText(paragraph)).ToBeVisibleAsync();

    public async Task ExpectLink(string linkText) => await Assertions
        .Expect(Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = linkText }).First).ToBeVisibleAsync();

    public async Task ExpectButton(string buttonText) => await Assertions
        .Expect(Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = buttonText }).First)
        .ToBeVisibleAsync();

    public async Task ExpectNotificationBanner(string heading, string message)
    {
        var banner = Page.Locator(".app-notification-banner");

        await Assertions.Expect(banner).ToBeVisibleAsync();
        await Assertions.Expect(banner.Locator(".govuk-notification-banner__title")).ToHaveTextAsync(heading);
        await Assertions.Expect(banner.Locator(".govuk-notification-banner__heading")).ToHaveTextAsync(message);
    }
}