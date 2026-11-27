using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests;

[TestFixture(Description = "Contributor permission tests")]
public sealed class ContributorsTests : PlaywrightTestBase
{
    private const string ContributorName = "Test Automation User";

    [TestCase(TestName = "caseworker can view any application, but not edit")]
    [CiRetry]
    public async Task CaseworkerCanViewButNotEditAsync()
    {
        var application = await CreateApplicationForTemplateAsync(ApiClient, ApiConfig.TemplateId);
        var taskList = new TaskListPage(Page);
        var standardFieldsTask = new StandardFieldsTask(Page);

        await LoginAsync("caseworker");
        await taskList.GoToAsync(application.ApplicationReference);
        await taskList.ExpectLoadedAsync();

        await standardFieldsTask.UnableToOpenAsync();
        await standardFieldsTask.GoToAsync(application.ApplicationReference, "full-name-page");
        await taskList.ExpectLoadedAsync();
    }

    [TestCase(TestName = "admin cannot submit another user's application")]
    [CiRetry]
    public async Task AdminCannotSubmitAnotherUsersApplicationAsync()
    {
        var application = await CreateApplicationForTemplateAsync(ApiClient, ApiConfig.TemplateId);
        var taskList = new TaskListPage(Page);

        await LoginAsync("admin");
        await taskList.GoToAsync(application.ApplicationReference);

        await taskList.ReviewApplicationAsync();

        var preview = new ApplicationPreviewPage(Page);
        await preview.ExpectNonLeadApplicantCannotSubmitAsync(Terminology.Singular);
    }

    [TestCase(TestName =
        "should be able to add a contributor and that contributor should be able to edit the application")]
    [CiRetry]
    public async Task AddContributorAndContributorCanEditAsync()
    {
        var application = await CreateApplicationForTemplateAsync(AdminApiClient, ApiConfig.TemplateId);
        var taskList = new TaskListPage(Page);
        var dashboardPage = new DashboardPage(Page);
        var contributorsPage = new ContributorsPage(Page);
        var contributorsInvitePage = new ContributorsInvitePage(Page);

        await LoginAsync("admin");
        await taskList.GoToAsync(application.ApplicationReference);

        await taskList.InviteContributorsAsync();

        await contributorsPage.AddContributorAsync();

        var contributorEmail = TestEnvironment.RequireEnvironmentVariable("DEFAULT_USER_EMAIL");
        await contributorsInvitePage.FillInviteAsync(ContributorName, contributorEmail);
        await contributorsInvitePage.SendInviteAsync();

        await contributorsPage.ExpectContributorAsync(2, ContributorName, contributorEmail);

        await LoginAsync("default");
        await dashboardPage.ExpectApplicationPresentAsync(application.ApplicationReference);
    }

    private static Task<CreateApplicationResponse> CreateApplicationForTemplateAsync(IAPIRequestContext request,
        string templateId) =>
        ApplicationApi.CreateApplicationAsync(request, ApplicationBuilder.CreateApplicationRequest(templateId));
}