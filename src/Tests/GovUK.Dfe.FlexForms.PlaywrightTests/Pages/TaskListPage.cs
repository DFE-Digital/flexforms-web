using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class TaskListPage(IPage page) : FormPage(page)
{
    public async Task ExpectLoadedAsync() => await Assertions.Expect(Page).ToHaveURLAsync(new Regex(@"/applications/[^/]+$"));

    public async Task ReviewApplicationAsync() => await ReviewApplicationButton().ClickAsync();

    private ILocator ReviewApplicationButton() => ById("review-application-button");
}
