using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService;

[TestFixture(Description = "Contributor permission tests")]
public sealed class ContributorsTests : PlaywrightTestBase
{
    private const string ContributorName = "Test Automation User";

    [TestCase(TestName = "caseworker can view any application, but not edit")]
    [CiRetry]
    public async Task CaseworkerCanViewButNotEditAsync()
    {
        var application = await CreateApplicationForTemplateAsync(ApiClient, ApiConfig.TemplateId);
        var applicationUrl = $"/applications/{application.ApplicationReference}";
        var taskList = new TaskListPage(Page);
        var standardFieldsTask = new StandardFieldsTask(Page);

        await LoginAsync("caseworker");
        await taskList.GoToAsync(application.ApplicationReference);
        await Assertions.Expect(Page).ToHaveURLAsync(applicationUrl);

        await standardFieldsTask.UnableToOpenAsync();
        await standardFieldsTask.GoToAsync(application.ApplicationReference, "full-name-page");
        await Assertions.Expect(Page).ToHaveURLAsync(applicationUrl);
    }

    [TestCase(TestName = "admin can edit any application")]
    [CiRetry]
    public async Task AdminCanEditAnyApplicationAsync()
    {
        var application = await CreateApplicationForTemplateAsync(ApiClient, ApiConfig.TemplateId);
        var applicationUrl = $"/applications/{application.ApplicationReference}";
        var taskList = new TaskListPage(Page);
        var standardFieldsTask = new StandardFieldsTask(Page);

        await LoginAsync("admin");
        await taskList.GoToAsync(application.ApplicationReference);
        await Assertions.Expect(Page).ToHaveURLAsync(applicationUrl);

        await standardFieldsTask.OpenAsync();
        await standardFieldsTask.CompleteAsync();
        await standardFieldsTask.ExpectCompletedAsync();
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

    [TestCase(TestName = "user should not be able to view an application that is not shared with them")]
    [CiRetry]
    public async Task UserCannotViewUnsharedApplicationAsync()
    {
        var application = await CreateApplicationForTemplateAsync(AdminApiClient, ApiConfig.TemplateId);
        var dashboardPage = new DashboardPage(Page);
        var applicationPage = new ApplicationPage(Page);

        await LoginAsync();
        await dashboardPage.GoToAsync();
        await dashboardPage.ExpectApplicationNotPresentAsync(application.ApplicationReference);

        await applicationPage.GoToAsync(application.ApplicationReference);
        await Assertions.Expect(Page).ToHaveURLAsync("Error/NotFound");
    }

    [TestCase(TestName =
        "should be able to add a contributor and that contributor should be able to edit the application")]
    [CiRetry]
    public async Task AddContributorAndContributorCanEditAsync()
    {
        var application = await CreateApplicationForTemplateAsync(AdminApiClient, ApiConfig.TemplateId);
        var applicationPage = new ApplicationPage(Page);
        var dashboardPage = new DashboardPage(Page);
        var contributorsPage = new ContributorsPage(Page);
        var contributorsInvitePage = new ContributorsInvitePage(Page);

        await LoginAsync("admin");
        await applicationPage.GoToAsync(application.ApplicationReference);

        await applicationPage.InviteContributorsAsync();

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