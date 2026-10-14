using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Components;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

[TestFixture(Description = "Admin application management")]
public sealed class ViewApplicationsTests : PlaywrightTestBase
{
    private CreateApplicationResponse _application = null!;
    private CreateApplicationResponse _applicationToDelete = null!;

    [OneTimeSetUp]
    public async Task CreateApplicationAsync()
    {
        var createRequest = ApplicationBuilder.CreateApplicationRequest(ApiConfig.TemplateId);
        _application = await ApplicationApi.CreateApplicationAsync(ApiClient, createRequest);
        _applicationToDelete = await ApplicationApi.CreateApplicationAsync(ApiClient, createRequest);
    }

    [SetUp]
    public async Task AdminLoginAndNavigateToViewApplicationsAsync()
    {
        await LoginAsync("admin");
        var adminPage = new AdminPage(Page);
        await adminPage.GoToAsync();
        await adminPage.OpenViewApplicationsAsync();
    }

    [TestCase(TestName = "Admin can view applications for a template")]
    [CiRetry]
    public async Task AdminCanViewApplicationsAsync()
    {
        var viewApplications = new ViewApplications(Page);

        await viewApplications.SelectATemplateAsync(TestData.DefaultTemplateName);
        await viewApplications.ApplicationsTable
            .HasTableHeaders(["Reference", "Application ID", "Date created", "Action"])
            .WithReference(_application.ApplicationReference)
            .ColumnHasValue("Reference", _application.ApplicationReference)
            .ColumnHasValue("Application ID", _application.ApplicationId)
            .ColumnContainsValue("Date created", ApplicationsTableFormatting.FormatApplicationDisplayDate())
            .ColumnHasValueWithLink(
                "Action",
                "Open",
                $"/applications/{_application.ApplicationReference}?templateId={ApiConfig.TemplateId}")
            .ColumnContainsValue("Action", "Delete")
            .VerifyAsync();
    }

    [TestCase(TestName = "Admin can delete an application and it is removed from the user's dashboard")]
    [CiRetry]
    public async Task AdminCanDeleteApplicationAndItIsRemovedFromUsersDashboardAsync()
    {
        var viewApplications = new ViewApplications(Page);
        var dashboardPage = new DashboardPage(Page);

        await viewApplications.SelectATemplateAsync(TestData.DefaultTemplateName);
        await viewApplications.DeleteApplicationAsync(_applicationToDelete.ApplicationReference);
        await viewApplications.ApplicationsTable
            .WithReference(_applicationToDelete.ApplicationReference)
            .ColumnHasValue("Action", "Deleted")
            .VerifyAsync();

        await dashboardPage.GoToAllApplicationsAsync();
        await dashboardPage.ApplicationsTable
            .WithReference(_applicationToDelete.ApplicationReference)
            .ColumnHasValue("Status", "Deleted")
            .VerifyAsync();

        // users should not be able to access deleted applications
        await LoginAsync("default");
        await dashboardPage.ExpectApplicationNotPresentAsync(_applicationToDelete.ApplicationReference);

        var taskList = new TaskListPage(Page);
        await taskList.GoToAsync(_applicationToDelete.ApplicationReference);
        await new ErrorPage(Page).ExpectPageNotFoundAsync();
    }
}