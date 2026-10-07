using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Components;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class DashboardPage : BasePage
{
    public DashboardPage(IPage page, Terminology terminology)
        : base(page, terminology)
    {
        ApplicationsTable = new ApplicationsTable(page);
    }

    public ApplicationsTable ApplicationsTable { get; }

    public async Task ChooseDefaultFormAsync()
    {
        await ById($"form-{TestEnvironment.RequireEnvironmentVariable("TEMPLATE_ID")}").ClickAsync();
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Go to dashboard" }).ClickAsync();
    }

    public async Task StartNewApplicationAsync() => await StartNewApplicationButton().ClickAsync();

    public async Task FilterApplicationsAsync() => await FilterApplicationsButton().ClickAsync();

    public async Task ApplyFiltersAsync() => await ApplyFilterButton().ClickAsync();

    public async Task FilterApplicationsByReferenceAsync(string reference) =>
        await FilterReferenceInput().FillAsync(reference);

    public async Task ExpectFilterApplicationsButtonAsync(string buttonText) =>
        await Assertions.Expect(FilterApplicationsButton()).ToHaveTextAsync(buttonText.Trim());

    public async Task ExpectFilterApplicationsButtonHiddenAsync() =>
        await Assertions.Expect(FilterApplicationsButton()).ToHaveCountAsync(0);

    public async Task ExpectStartNewButtonAsync(string buttonText) =>
        await Assertions.Expect(StartNewApplicationButton()).ToHaveTextAsync(buttonText);

    public async Task ExpectApplicationPresentAsync(string reference) =>
        await Assertions.Expect(ApplicationLink(reference)).ToBeVisibleAsync();

    public async Task ExpectApplicationNotPresentAsync(string reference) =>
        await Assertions.Expect(ApplicationLink(reference)).ToHaveCountAsync(0);

    public async Task ExpectPreviewBannerVisibleAsync(string formName) =>
        await Assertions.Expect(PreviewBanner(formName)).ToBeVisibleAsync();

    public async Task ExpectPreviewBannerHiddenAsync(string formName) =>
        await Assertions.Expect(PreviewBanner(formName)).ToHaveCountAsync(0);

    private ILocator StartNewApplicationButton() => ById("start-new-application-button");

    private ILocator FilterApplicationsButton() => Page.GetByTestId("filter-applications-button");

    private ILocator ApplyFilterButton() =>
        Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Apply filters" });

    private ILocator FilterReferenceInput() => Page.GetByLabel("Reference number");

    private ILocator PreviewBanner(string formName) => Page.GetByText($"THIS IS A PREVIEW OF {formName}");

    private ILocator ApplicationLink(string reference) =>
        Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = reference });
}