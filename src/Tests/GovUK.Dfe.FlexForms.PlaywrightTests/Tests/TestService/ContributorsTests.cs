using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;
using NUnit.Framework;

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

        await LoginAsync("caseworker");
        await Page.GotoAsync(applicationUrl);
        await Assertions.Expect(Page).ToHaveURLAsync(applicationUrl);

        var standardFieldsTask = new StandardFieldsTask(Page);
        await standardFieldsTask.UnableToOpenAsync();
        await Page.GotoAsync($"{applicationUrl}/standard-fields/full-name-page");
        await Assertions.Expect(Page).ToHaveURLAsync(applicationUrl);
    }

    [TestCase(TestName = "admin can edit any application")]
    [CiRetry]
    public async Task AdminCanEditAnyApplicationAsync()
    {
        var application = await CreateApplicationForTemplateAsync(ApiClient, ApiConfig.TemplateId);
        var applicationUrl = $"/applications/{application.ApplicationReference}";

        await LoginAsync("admin");
        await Page.GotoAsync(applicationUrl);
        await Assertions.Expect(Page).ToHaveURLAsync(applicationUrl);

        var standardFieldsTask = new StandardFieldsTask(Page);
        await standardFieldsTask.OpenAsync();
        await standardFieldsTask.CompleteAsync();
        await standardFieldsTask.ExpectCompletedAsync();
    }

    [TestCase(TestName = "admin cannot submit another user's application")]
    [CiRetry]
    public async Task AdminCannotSubmitAnotherUsersApplicationAsync()
    {
        var application = await CreateApplicationForTemplateAsync(ApiClient, ApiConfig.TemplateId);

        await LoginAsync("admin");
        await Page.GotoAsync($"/applications/{application.ApplicationReference}");

        var taskList = new TaskListPage(Page);
        await taskList.ReviewApplicationAsync();

        var preview = new ApplicationPreviewPage(Page);
        await preview.ExpectNonLeadApplicantCannotSubmitAsync(Terminology.Singular);
    }

    [TestCase(TestName = "user should not be able to view an application that is not shared with them")]
    [CiRetry]
    public async Task UserCannotViewUnsharedApplicationAsync()
    {
        var application = await CreateApplicationForTemplateAsync(AdminApiClient, ApiConfig.TemplateId);
        var dashboardPage = new DashboardPage(Page, Terminology);

        await LoginAsync();
        await Page.GotoAsync("/");
        await dashboardPage.ExpectApplicationNotPresentAsync(application.ApplicationReference);

        await Page.GotoAsync($"/applications/{application.ApplicationReference}");
        await Assertions.Expect(Page).ToHaveURLAsync("Error/NotFound");
    }

    [TestCase(TestName = "should be able to add a contributor and that contributor should be able to edit the application")]
    [CiRetry]
    public async Task AddContributorAndContributorCanEditAsync()
    {
        var application = await CreateApplicationForTemplateAsync(AdminApiClient, ApiConfig.TemplateId);
        var applicationPage = new ApplicationPage(Page, Terminology);
        var dashboardPage = new DashboardPage(Page, Terminology);
        var contributorsPage = new ContributorsPage(Page, Terminology);
        var contributorsInvitePage = new ContributorsInvitePage(Page, Terminology);

        await LoginAsync("admin");
        await Page.GotoAsync($"/applications/{application.ApplicationReference}");

        await applicationPage.InviteContributorsAsync();

        await contributorsPage.AddContributorAsync();

        var contributorEmail = TestEnvironment.RequireEnvironmentVariable("DEFAULT_USER_EMAIL");
        await contributorsInvitePage.FillInviteAsync(ContributorName, contributorEmail);
        await contributorsInvitePage.SendInviteAsync();

        await contributorsPage.ExpectContributorAsync(2, ContributorName, contributorEmail);

        await LoginAsync("default");
        await dashboardPage.ExpectApplicationPresentAsync(application.ApplicationReference);
    }

    private static Task<CreateApplicationResponse> CreateApplicationForTemplateAsync(IAPIRequestContext request, string templateId) =>
        ApplicationApi.CreateApplicationAsync(request, ApplicationBuilder.CreateApplicationRequest(templateId));
}
