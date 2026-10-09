using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

[NonParallelizable]
[TestFixture(Description = "Template live status")]
public sealed class TemplateLiveStatusTests : PlaywrightTestBase
{
    [TestCase(TestName = "Admin can make a form live")]
    [CiRetry]
    public async Task AdminCanMakeFormLiveAsync()
    {
        var adminPage = new AdminPage(Page);
        var chooseFormPage = new ChooseFormPage(Page);
        var dashboardPage = new DashboardPage(Page);

        await LoginAsync("admin");
        await adminPage.GoToAsync();

        await adminPage.MakeTemplateLiveAsync(TestData.LiveSwitchOnlyTemplateName);
        await adminPage.ExpectTemplateLiveAsync(TestData.LiveSwitchOnlyTemplateName);

        await chooseFormPage.NavigateToAsync(NavigationSection.Forms);
        await chooseFormPage.SelectFormAsync(TestData.LiveSwitchOnlyTemplateName, "Live");
        await chooseFormPage.GoToDashboardAsync();

        await dashboardPage.ExpectPreviewBannerHiddenAsync(TestData.LiveSwitchOnlyTemplateName);
    }

    [TestCase(TestName = "Admin can make a form not live")]
    [CiRetry]
    public async Task AdminCanMakeFormNotLiveAsync()
    {
        await Templates.UpdateTemplateLiveAsync(AdminApiClient, TestData.LiveSwitchOnlyTemplateName, true);
        var adminPage = new AdminPage(Page);
        var chooseFormPage = new ChooseFormPage(Page);
        var dashboardPage = new DashboardPage(Page);

        await LoginAsync("admin");
        await adminPage.GoToAsync();

        await adminPage.MakeTemplateNotLiveAsync(TestData.LiveSwitchOnlyTemplateName);
        await adminPage.ExpectTemplateNotLiveAsync(TestData.LiveSwitchOnlyTemplateName);

        await chooseFormPage.NavigateToAsync(NavigationSection.Forms);
        await chooseFormPage.SelectFormAsync(TestData.LiveSwitchOnlyTemplateName, "Not live");
        await chooseFormPage.GoToDashboardAsync();

        await dashboardPage.ExpectPreviewBannerVisibleAsync(TestData.LiveSwitchOnlyTemplateName);
    }

    [OneTimeTearDown]
    public async Task RestoreTemplateToNotLiveAsync()
    {
        await Templates.UpdateTemplateLiveAsync(AdminApiClient, TestData.LiveSwitchOnlyTemplateName, false);
    }
}