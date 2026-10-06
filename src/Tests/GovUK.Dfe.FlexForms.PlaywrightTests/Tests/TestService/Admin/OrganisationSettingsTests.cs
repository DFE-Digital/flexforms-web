using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

public sealed class OrganisationSettingsTests : PlaywrightTestBase
{
    private CreateApplicationResponse _application = null!;

    [OneTimeSetUp]
    public async Task CreateApplicationWithContributorAsync()
    {
        var createRequest = ApplicationBuilder.CreateApplicationRequest(ApiConfig.TemplateId);
        _application = await ApplicationApi.CreateApplicationAsync(AdminApiClient, createRequest);
    }

    [TestCase(TestName = "Admin can update the application terminology")]
    [CiRetry]
    public async Task AdminCanUpdateTheApplicationTerminologyAsync()
    {
        const string singular = "ApplicationTest";
        const string plural = "ApplicationsTest";

        var adminPage = new AdminPage(Page, Terminology);
        var organisationSettings = new OrganisationSettings(Page, Terminology);
        var dashboardPage = new DashboardPage(Page, Terminology);

        await LoginAsync("admin");
        await Page.GotoAsync("/admin");
        await adminPage.OpenOrganisationSettingsAsync();

        await organisationSettings.SetApplicationTerminologyAsync(singular, plural);
        await organisationSettings.SaveSettingsAsync();

        await Page.GotoAsync("/");
        await dashboardPage.ExpectHeading($"Your {plural}");
        await dashboardPage.ExpectHeading($"{plural} in progress");
        await dashboardPage.ExpectFilterApplicationsButtonAsync($"Filter {plural}");
        await dashboardPage.ExpectLink($"Continue {singular}");
        await dashboardPage.ExpectHeading($"Start a new {singular}");
        await Page.GotoAsync($"/applications/{_application.ApplicationReference}");
        await dashboardPage.ExpectHeading($"Your {singular}");
        await dashboardPage.ExpectParagraph($"{singular} reference:");
        await dashboardPage.ExpectParagraph($"{singular} version:");
    }

    [TearDown]
    public async Task RestoreDefaultTerminologyAsync()
    {
        await TenantAdmin.RestoreApplicationTerminologyAsync(
            AdminApiClient,
            ApiConfig.TenantId,
            Terminology.Singular,
            Terminology.Plural);

        // Web cache needs to be cleared for the terminology to be updated in the UI
        await LoginAsync("admin");
        await new TenantSettings(Page, Terminology).RefreshSettingsAsync();
    }
}