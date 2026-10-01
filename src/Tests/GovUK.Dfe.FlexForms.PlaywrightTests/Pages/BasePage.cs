using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
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

public abstract class BasePage(IPage page, Terminology terminology)
{
    protected IPage Page { get; } = page;

    protected Terminology Terminology { get; } = terminology;

    protected ILocator ById(string id) => Page.Locator($"[id=\"{id}\"]");

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
}