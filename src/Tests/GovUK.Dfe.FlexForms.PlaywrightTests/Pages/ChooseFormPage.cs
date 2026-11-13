using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class ChooseFormPage(IPage page) : BasePage(page)
{
    public async Task GoToAsync() => await Page.GotoAsync("/templates");

    public async Task SelectFormAsync(string name, string liveStatus)
    {
        var formName = $"{name.Trim()} ({liveStatus.Trim()})";

        await Page
            .GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = formName })
            .CheckAsync();
    }

    public async Task GoToDashboardAsync()
    {
        await GoToDashboardButton().ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync("/applications/dashboard");
    }

    public async Task ExpectFormCanBeChosenAsync(string templateId)
    {
        await Assertions.Expect(FormRadio(templateId)).ToBeEnabledAsync();
        await Assertions.Expect(NoAccessHint(templateId)).ToHaveCountAsync(0);
    }

    public async Task ExpectFormCannotBeChosenAsync(string templateId)
    {
        await Assertions.Expect(FormRadio(templateId)).ToBeDisabledAsync();
        await Assertions.Expect(NoAccessHint(templateId)).ToHaveTextAsync("You do not have access to this template");
    }

    /// <summary>Enables a greyed-out form and submits it, as someone getting past the disabled radio would.</summary>
    public async Task ForceChooseFormAsync(string templateId)
    {
        await FormRadio(templateId).EvaluateAsync("radio => { radio.disabled = false; radio.checked = true; }");
        await GoToDashboardButton().ClickAsync();
    }

    public async Task ExpectErrorAsync(string message)
    {
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/templates"));
        await Assertions.Expect(Page.Locator(".govuk-error-summary")).ToContainTextAsync(message);
    }

    private ILocator FormRadio(string templateId) => ById($"form-{templateId}");

    private ILocator NoAccessHint(string templateId) => ById($"form-{templateId}-no-access");

    private ILocator GoToDashboardButton() =>
        Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Go to dashboard" });
}
