using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Components;
using NUnit.Framework;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.Transfers;

[TestFixture(Description = "Transfers smoke")]
public sealed class SmokeTests : PlaywrightTestBase
{
    private const string ContributorName = "Playwright Test";
    private const string ContributorEmail = "playwright@test.com";

    private CreateApplicationResponse _application = null!;
    private string _expectedCreatedStatus = null!;

    [OneTimeSetUp]
    public async Task SmokeOneTimeSetUpAsync()
    {
        var applicationRequest = ApplicationBuilder.CreateApplicationRequest(ApiConfig.TemplateId);
        _application = await ApplicationApi.CreateApplicationAsync(ApiClient, applicationRequest);

        var customApplicationStatusResponse = await Templates.GetTemplateCustomStatusesAsync(ApiClient, ApiConfig.TemplateId);
        _expectedCreatedStatus = customApplicationStatusResponse
            .FirstOrDefault(status => status.ApplicationStatus == ApplicationStatus.Created)?.Label ?? "Created";
    }

    [SetUp]
    public async Task LoginAsDefaultUserAsync() => await LoginAsync();

    [TestCase(TestName = "should add a contributor")]
    [CiRetry]
    public async Task ShouldAddAContributorAsync()
    {
        var dashboardPage = new DashboardPage(Page, Terminology);
        var contributorsPage = new ContributorsPage(Page, Terminology);
        var contributorsInvitePage = new ContributorsInvitePage(Page, Terminology);

        await dashboardPage.StartNewApplicationAsync();
        await contributorsPage.AddContributorAsync();

        await contributorsInvitePage.FillInviteAsync(ContributorName, ContributorEmail);
        await contributorsInvitePage.SendInviteAsync();

        await contributorsPage.ExpectContributorAsync(2, ContributorName, ContributorEmail);
    }

    [TestCase(TestName = "should filter applications by reference number")]
    [CiRetry]
    public async Task ShouldFilterApplicationsByReferenceNumberAsync()
    {
        var dashboardPage = new DashboardPage(Page, Terminology);

        await dashboardPage.FilterApplicationsAsync();
        await dashboardPage.FilterApplicationsByReferenceAsync(_application.ApplicationReference);
        await dashboardPage.ApplyFiltersAsync();

        await dashboardPage.ApplicationsTable
            .HasTableHeaders(["Reference number", "Date started", "Date submitted", "Status", "Action"])
            .HasNumberOfRows(1)
            .WithReference(_application.ApplicationReference)
            .ColumnHasValue("Reference number", _application.ApplicationReference)
            .ColumnHasValue("Date started", ApplicationsTableFormatting.FormatApplicationDisplayDate())
            .ColumnHasValue("Date submitted", "Not submitted")
            .ColumnHasValue("Status", _expectedCreatedStatus)
            .ColumnHasValueWithLink(
                "Action",
                $"Continue {Terminology.Singular}",
                $"/applications/{_application.ApplicationReference}")
            .VerifyAsync();
    }
}
