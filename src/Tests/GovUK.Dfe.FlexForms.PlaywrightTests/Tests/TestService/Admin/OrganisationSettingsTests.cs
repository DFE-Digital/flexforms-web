using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

[TestFixture(Description = "Organisation settings")]
[NonParallelizable] // modifies tenant config
public sealed class OrganisationSettingsTests : PlaywrightTestBase
{
    private CreateApplicationResponse _application = null!;
    private CreateApplicationResponse _otherTemplateApplication = null!;

    [OneTimeSetUp]
    public async Task CreateApplicationWithContributorAsync()
    {
        var createRequest = ApplicationBuilder.CreateApplicationRequest(ApiConfig.TemplateId);
        _application = await ApplicationApi.CreateApplicationAsync(AdminApiClient, createRequest);

        var otherTemplateId =
            await Templates.GetTemplateIdByNameAsync(AdminApiClient, TestData.LiveSwitchOnlyTemplateName);
        var otherTemplateApplicationRequest = ApplicationBuilder.CreateApplicationRequest(otherTemplateId);
        _otherTemplateApplication =
            await ApplicationApi.CreateApplicationAsync(AdminApiClient, otherTemplateApplicationRequest);

        // ensure at least 6 applications exist so that the dashboard page size can be tested
        for (var i = 0; i < 5; i++)
        {
            await ApplicationApi.CreateApplicationAsync(AdminApiClient, createRequest);
        }
    }

    [SetUp]
    public async Task AdminLoginAndNavigateToOrganisationSettingsAsync()
    {
        await LoginAsync("admin");
        var adminPage = new AdminPage(Page);
        await adminPage.GoToAsync();
        await adminPage.OpenOrganisationSettingsAsync();
    }

    [TestCase(TestName = "Admin can update the application terminology")]
    [CiRetry]
    public async Task AdminCanUpdateTheApplicationTerminologyAsync()
    {
        const string singular = "ApplicationTest";
        const string plural = "ApplicationsTest";

        var organisationSettings = new OrganisationSettings(Page);
        var dashboardPage = new DashboardPage(Page);
        var applicationPage = new ApplicationPage(Page);

        await organisationSettings.ApplicationTerminology
            .WithSingular(singular)
            .WithPlural(plural)
            .SaveAsync();

        await dashboardPage.GoToAsync();
        await dashboardPage.ExpectHeading($"Your {plural}");
        await dashboardPage.ExpectHeading($"{plural} in progress");
        await dashboardPage.ExpectFilterApplicationsButtonAsync($"Filter {plural}");
        await dashboardPage.ExpectLink($"Continue {singular}");
        await dashboardPage.ExpectHeading($"Start a new {singular}");
        await applicationPage.GoToAsync(_application.ApplicationReference);
        await dashboardPage.ExpectHeading($"Your {singular}");
        await dashboardPage.ExpectText($"{singular} reference:");
        await dashboardPage.ExpectText($"{singular} version:");
    }

    [TestCase(TestName = "Admin can set the notification banner")]
    [CiRetry]
    public async Task AdminCanSetTheNotificationBannerAsync()
    {
        const string heading = "Test Banner Heading";
        const string message = "Test Banner Message";
        var organisationSettings = new OrganisationSettings(Page);
        var dashboardPage = new DashboardPage(Page);
        var applicationPage = new ApplicationPage(Page);
        var adminPage = new AdminPage(Page);

        await organisationSettings.NotificationBanner
            .Enabled()
            .WithHeading(heading)
            .WithMessage(message)
            .SaveAsync();

        await dashboardPage.GoToAsync();
        await organisationSettings.ExpectNotificationBanner(heading, message);

        await applicationPage.GoToAsync(_application.ApplicationReference);
        await organisationSettings.ExpectNotificationBanner(heading, message);

        await adminPage.GoToAsync();
        await organisationSettings.ExpectNotificationBanner(heading, message);
    }

    [TestCase(TestName = "Admin can customise the dashboard config")]
    [CiRetry]
    public async Task AdminCanCustomiseTheDashboardConfigAsync()
    {
        var organisationSettings = new OrganisationSettings(Page);
        var dashboardPage = new DashboardPage(Page);
        var applicationPage = new ApplicationPage(Page);

        await organisationSettings.Dashboard
            .WithPageSize(5)
            .WithFiltersDisabled()
            .WithMainHeading("Your Bananas")
            .WithInProgressHeading("Bananas in progress")
            .WithStartNewHeading("Start a new Banana")
            .WithStartNewHint("Here is a hint for starting a new Banana")
            .WithStartNewButtonText("Create banana")
            .SaveAsync();

        await dashboardPage.GoToAsync();
        await dashboardPage.ApplicationsTable.HasNumberOfRows(5).VerifyAsync();
        await dashboardPage.ExpectFilterApplicationsButtonHiddenAsync();
        await dashboardPage.ExpectHeading("Your Bananas");
        await dashboardPage.ExpectHeading("Bananas in progress");
        await dashboardPage.ExpectHeading("Start a new Banana");
        await dashboardPage.ExpectText("Here is a hint for starting a new Banana");
        await dashboardPage.ExpectStartNewButtonAsync("Create banana");

        // application still uses the default terminology
        await applicationPage.GoToAsync(_application.ApplicationReference);
        await dashboardPage.ExpectHeading($"Your {Terminology.Singular}");
        await dashboardPage.ExpectText($"{Terminology.Singular} reference:");
        await dashboardPage.ExpectText($"{Terminology.Singular} version:");
    }

    [TestCase(TestName = "Admin can customise check your answers config")]
    [CiRetry]
    public async Task AdminCanCustomiseCheckYourAnswersConfigAsync()
    {
        var organisationSettings = new OrganisationSettings(Page);
        var applicationPreviewPage = new ApplicationPreviewPage(Page);

        await organisationSettings.CheckYourAnswersSettings
            .WithPageHeading("Check your apple answers")
            .WithSubmitHeading("Submit your apple answers")
            .WithSubmitHint("Here is a hint for submitting your apple answers")
            .WithSubmitButtonText("Submit your apple answers")
            .SaveAsync();

        await applicationPreviewPage.GoToAsync(_application.ApplicationReference);
        await applicationPreviewPage.ExpectHeading("Check your apple answers");
        await applicationPreviewPage.ExpectHeading("Submit your apple answers");
        await applicationPreviewPage.ExpectText("Here is a hint for submitting your apple answers");
        await applicationPreviewPage.ExpectButton("Submit your apple answers");
    }

    [TestCase(TestName = "Admin can customise application submitted page for all templates")]
    [CiRetry]
    public async Task AdminCanCustomiseApplicationSubmittedPageAsync()
    {
        var organisationSettings = new OrganisationSettings(Page);
        var applicationSubmittedPage = new ApplicationSubmittedPage(Page);

        await organisationSettings.ApplicationSubmittedSettings.ForAllTemplates()
            .WithConfirmationTitle("Orange app submitted")
            .WithPageBody(
                "## Next steps for your orange application\n\nThank you for submitting your orange application. We will review it and get back to you shortly.")
            .SaveAsync();

        await applicationSubmittedPage.GoToAsync(_application.ApplicationReference);
        await applicationSubmittedPage.ExpectContentAsync("Orange app submitted",
            "Next steps for your orange application",
            "Thank you for submitting your orange application. We will review it and get back to you shortly.");
    }

    [TestCase(TestName = "Admin can customise application submitted page for a specific template")]
    [CiRetry]
    public async Task AdminCanCustomiseApplicationSubmittedPageForSpecificTemplateAsync()
    {
        var organisationSettings = new OrganisationSettings(Page);
        var applicationSubmittedPage = new ApplicationSubmittedPage(Page);

        await organisationSettings.ApplicationSubmittedSettings.ForTemplate(ApiConfig.TemplateId)
            .WithConfirmationTitle("Pear app submitted")
            .WithPageBody(
                "## Next steps for your pear application\n\nThank you for submitting your pear application. We will review it and get back to you shortly.")
            .SaveAsync();

        await applicationSubmittedPage.GoToAsync(_application.ApplicationReference);
        await applicationSubmittedPage.ExpectContentAsync("Pear app submitted",
            "Next steps for your pear application",
            "Thank you for submitting your pear application. We will review it and get back to you shortly.");

        // verify other template application still uses the default submitted page
        await applicationSubmittedPage.GoToAsync(_otherTemplateApplication.ApplicationReference);
        await applicationSubmittedPage.ExpectHeading("Application submitted");
        await applicationSubmittedPage.ExpectHeading("What happens next");
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
        await TenantAdmin.RestoreApplicationSubmittedPageAsync(AdminApiClient, ApiConfig.TenantId);
        await TenantAdmin.RefreshTenantSettingsAsync(AdminApiClient);

        // Web cache needs to be cleared for the restored settings to show in the UI
        await LoginAsync("admin");
        await new TenantSettings(Page).RefreshSettingsAsync();
    }
}