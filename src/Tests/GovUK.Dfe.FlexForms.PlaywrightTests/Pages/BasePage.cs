using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public abstract class BasePage(IPage page, Terminology terminology)
{
    protected IPage Page { get; } = page;

    protected Terminology Terminology { get; } = terminology;

    protected ILocator ById(string id) => Page.Locator($"[id=\"{id}\"]");
}
