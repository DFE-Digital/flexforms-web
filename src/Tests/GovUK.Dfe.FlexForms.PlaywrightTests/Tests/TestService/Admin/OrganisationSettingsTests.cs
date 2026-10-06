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

    [SetUp]
    public async Task AdminLoginAndNavigateToOrganisationSettingsAsync()
    {
        await LoginAsync("admin");
        await Page.GotoAsync("/admin");
        await new AdminPage(Page, Terminology).OpenOrganisationSettingsAsync();
    }

    [TestCase(TestName = "Admin can update the application terminology")]
    [CiRetry]
    public async Task AdminCanUpdateTheApplicationTerminologyAsync()
    {
        const string singular = "ApplicationTest";
        const string plural = "ApplicationsTest";

        var organisationSettings = new OrganisationSettings(Page, Terminology);
        var dashboardPage = new DashboardPage(Page, Terminology);

        await organisationSettings.ApplicationTerminology
            .WithSingular(singular)
            .WithPlural(plural)
            .SaveAsync();

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

    [TestCase(TestName = "Admin can set the notification banner")]
    [CiRetry]
    public async Task AdminCanSetTheNotificationBannerAsync()
    {
        const string heading = "Test Banner Heading";
        const string message = "Test Banner Message";
        var organisationSettings = new OrganisationSettings(Page, Terminology);

        await organisationSettings.NotificationBanner
            .Enabled()
            .WithHeading(heading)
            .WithMessage(message)
            .SaveAsync();

        await Page.GotoAsync("/");
        await organisationSettings.ExpectNotificationBanner(heading, message);

        await Page.GotoAsync($"/applications/{_application.ApplicationReference}");
        await organisationSettings.ExpectNotificationBanner(heading, message);

        await Page.GotoAsync("/admin");
        await organisationSettings.ExpectNotificationBanner(heading, message);
    }

    [TestCase(TestName = "Admin can customise the dashboard config")]
    [CiRetry]
    public async Task AdminCanCustomiseTheDashboardConfigAsync()
    {
        var organisationSettings = new OrganisationSettings(Page, Terminology);
        var dashboardPage = new DashboardPage(Page, Terminology);

        await organisationSettings.Dashboard
            .WithPageSize(5)
            .WithFiltersDisabled()
            .WithMainHeading("Your Bananas")
            .WithInProgressHeading("Bananas in progress")
            .WithStartNewHeading("Start a new Banana")
            .WithStartNewHint("Here is a hint for starting a new Banana")
            .WithStartNewButtonText("Create banana")
            .SaveAsync();

        await Page.GotoAsync("/");
        await dashboardPage.ApplicationsTable.HasNumberOfRows(5).VerifyAsync();
        await dashboardPage.ExpectFilterApplicationsButtonHiddenAsync();
        await dashboardPage.ExpectHeading("Your Bananas");
        await dashboardPage.ExpectHeading("Bananas in progress");
        await dashboardPage.ExpectHeading("Start a new Banana");
        await dashboardPage.ExpectParagraph("Here is a hint for starting a new Banana");
        await dashboardPage.ExpectStartNewButtonAsync("Create banana");

        // application still uses the default terminology
        await Page.GotoAsync($"/applications/{_application.ApplicationReference}");
        await dashboardPage.ExpectHeading($"Your {Terminology.Singular}");
        await dashboardPage.ExpectParagraph($"{Terminology.Singular} reference:");
        await dashboardPage.ExpectParagraph($"{Terminology.Singular} version:");
    }

    [TestCase(TestName = "Admin can customise check your answers config")]
    [CiRetry]
    public async Task AdminCanCustomiseCheckYourAnswersConfigAsync()
    {
        var organisationSettings = new OrganisationSettings(Page, Terminology);
        var applicationPage = new ApplicationPage(Page, Terminology);

        await organisationSettings.CheckYourAnswersSettings
            .WithPageHeading("Check your apple answers")
            .WithSubmitHeading("Submit your apple answers")
            .WithSubmitHint("Here is a hint for submitting your apple answers")
            .WithSubmitButtonText("Submit your apple answers")
            .SaveAsync();

        await Page.GotoAsync($"/applications/{_application.ApplicationReference}?preview=true");
        await applicationPage.ExpectHeading("Check your apple answers");
        await applicationPage.ExpectHeading("Submit your apple answers");
        await applicationPage.ExpectParagraph("Here is a hint for submitting your apple answers");
        await applicationPage.ExpectButton("Submit your apple answers");
    }

    [TearDown]
    public async Task RestoreDefaultTerminologyAsync()
    {
        await TenantAdmin.RestoreApplicationTerminologyAsync(
            AdminApiClient,
            ApiConfig.TenantId,
            Terminology.Singular,
            Terminology.Plural);
        await TenantAdmin.ClearNotificationBannerAsync(AdminApiClient, ApiConfig.TenantId);
        await TenantAdmin.RestoreDashboardAsync(AdminApiClient, ApiConfig.TenantId);
        await TenantAdmin.RestoreApplicationPreviewAsync(AdminApiClient, ApiConfig.TenantId);

        // Web cache needs to be cleared for the restored settings to show in the UI
        await LoginAsync("admin");
        await new TenantSettings(Page, Terminology).RefreshSettingsAsync();
    }
}