using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

[TestFixture(Description = "Template live status")]
public sealed class TemplateLiveStatusTests : PlaywrightTestBase
{
    private const string TemplateName = "live switch only";

    [TestCase(TestName = "Admin can make a form live")]
    [CiRetry]
    public async Task AdminCanMakeFormLiveAsync()
    {
        var adminPage = new AdminPage(Page);
        var chooseFormPage = new ChooseFormPage(Page);
        var dashboardPage = new DashboardPage(Page);

        await LoginAsync("admin");
        await adminPage.GoToAsync();

        await adminPage.MakeTemplateLiveAsync(TemplateName);
        await adminPage.ExpectTemplateLiveAsync(TemplateName);

        await chooseFormPage.GoToAsync(NavigationSection.Forms);
        await chooseFormPage.SelectFormAsync(TemplateName, "Live");
        await chooseFormPage.GoToDashboardAsync();

        await dashboardPage.ExpectPreviewBannerHiddenAsync(TemplateName);
    }

    [TestCase(TestName = "Admin can make a form not live")]
    [CiRetry]
    public async Task AdminCanMakeFormNotLiveAsync()
    {
        await Templates.UpdateTemplateLiveAsync(AdminApiClient, TemplateName, true);
        var adminPage = new AdminPage(Page);
        var chooseFormPage = new ChooseFormPage(Page);
        var dashboardPage = new DashboardPage(Page);

        await LoginAsync("admin");
        await adminPage.GoToAsync();

        await adminPage.MakeTemplateNotLiveAsync(TemplateName);
        await adminPage.ExpectTemplateNotLiveAsync(TemplateName);

        await chooseFormPage.GoToAsync(NavigationSection.Forms);
        await chooseFormPage.SelectFormAsync(TemplateName, "Not live");
        await chooseFormPage.GoToDashboardAsync();

        await dashboardPage.ExpectPreviewBannerVisibleAsync(TemplateName);
    }

    [OneTimeTearDown]
    public async Task RestoreTemplateToNotLiveAsync()
    {
        await Templates.UpdateTemplateLiveAsync(AdminApiClient, TemplateName, false);
    }
}