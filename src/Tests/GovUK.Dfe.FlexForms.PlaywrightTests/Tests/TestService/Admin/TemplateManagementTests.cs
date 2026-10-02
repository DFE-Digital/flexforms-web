using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

public sealed class TemplateManagementTests : PlaywrightTestBase
{
    private const string TemplateName = "default";

    private static readonly IReadOnlyDictionary<ApplicationStatus, string> BaseStatusLabels =
        new Dictionary<ApplicationStatus, string>
        {
            [ApplicationStatus.Created] = "Created",
            [ApplicationStatus.InProgress] = "In progress",
            [ApplicationStatus.Submitted] = "Submitted",
            [ApplicationStatus.Deleted] = "Deleted",
        };

    private static readonly IReadOnlyDictionary<ApplicationStatus, string> CustomStatusLabels =
        new Dictionary<ApplicationStatus, string>
        {
            [ApplicationStatus.Created] = "Playwright created label",
            [ApplicationStatus.InProgress] = "Playwright in progress label",
            [ApplicationStatus.Submitted] = "Playwright submitted label",
            [ApplicationStatus.Deleted] = "Playwright deleted label",
        };

    private CreateApplicationResponse _createdApplication = null!;
    private CreateApplicationResponse _inProgressApplication = null!;
    private CreateApplicationResponse _submittedApplication = null!;
    private CreateApplicationResponse _deletedApplication = null!;

    [OneTimeSetUp]
    public async Task CreateApplicationsForStatusVerificationAsync()
    {
        var createRequest = ApplicationBuilder.CreateApplicationRequest(ApiConfig.TemplateId);
        _createdApplication = await ApplicationApi.CreateApplicationAsync(AdminApiClient, createRequest);

        _inProgressApplication = await ApplicationApi.CreateApplicationAsync(AdminApiClient, createRequest);
        await ApplicationApi.AddApplicationResponseAsync(
            AdminApiClient,
            _inProgressApplication.ApplicationId,
            """{"schoolName":"Playwright status test"}""");

        _submittedApplication = await ApplicationApi.CreateApplicationAsync(AdminApiClient, createRequest);
        await ApplicationApi.AddApplicationResponseAsync(
            AdminApiClient,
            _submittedApplication.ApplicationId,
            """{"schoolName":"Playwright status test"}""");
        await ApplicationApi.SubmitApplicationAsync(AdminApiClient, _submittedApplication.ApplicationId);

        _deletedApplication = await ApplicationApi.CreateApplicationAsync(AdminApiClient, createRequest);
        await ApplicationApi.DeleteApplicationAsync(AdminApiClient, _deletedApplication.ApplicationId);
    }

    [OneTimeTearDown]
    public async Task RestoreBaseStatusLabelsAsync()
    {
        foreach (var (status, label) in BaseStatusLabels)
        {
            await Templates.CreateCustomApplicationStatusAsync(AdminApiClient, ApiConfig.TemplateId, status, label);
        }
    }

    [TestCase(TestName =
        "Admin can update custom status labels for a template and see them reflected on the dashboard")]
    [CiRetry]
    public async Task CustomStatusLabelsAppearOnTemplateDashboardAsync()
    {
        var statusOverridesPage = new CustomStatusLabelOverridesPage(Page, Terminology);
        var chooseFormPage = new ChooseFormPage(Page, Terminology);
        var dashboardPage = new DashboardPage(Page, Terminology);

        await LoginAsync("admin");
        await statusOverridesPage.OpenForTemplateAsync(ApiConfig.TemplateId);

        foreach (var (status, label) in CustomStatusLabels)
        {
            await statusOverridesPage.SaveCustomStatusLabelAsync(status, label);
        }

        await chooseFormPage.GoToAsync(NavigationSection.Forms);
        await chooseFormPage.SelectFormAsync(TemplateName, "Live");
        await chooseFormPage.GoToDashboardAsync();
        await dashboardPage.FilterApplicationsAsync();

        await AssertApplicationStatusOnDashboardAsync(
            dashboardPage,
            _createdApplication.ApplicationReference,
            CustomStatusLabels[ApplicationStatus.Created]);

        await AssertApplicationStatusOnDashboardAsync(
            dashboardPage,
            _inProgressApplication.ApplicationReference,
            CustomStatusLabels[ApplicationStatus.InProgress]);

        await AssertApplicationStatusOnDashboardAsync(
            dashboardPage,
            _submittedApplication.ApplicationReference,
            CustomStatusLabels[ApplicationStatus.Submitted]);

        await AssertApplicationStatusOnDashboardAsync(
            dashboardPage,
            _deletedApplication.ApplicationReference,
            CustomStatusLabels[ApplicationStatus.Deleted]);
    }

    private static async Task AssertApplicationStatusOnDashboardAsync(
        DashboardPage dashboardPage,
        string reference,
        string expectedStatus)
    {
        await dashboardPage.FilterApplicationsByReferenceAsync(reference);
        await dashboardPage.ApplyFiltersAsync();

        await dashboardPage.ApplicationsTable
            .HasNumberOfRows(1)
            .WithReference(reference)
            .ColumnHasValue("Status", expectedStatus)
            .VerifyAsync();
    }
}