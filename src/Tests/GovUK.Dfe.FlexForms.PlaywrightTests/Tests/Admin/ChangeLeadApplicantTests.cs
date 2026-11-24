using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.Admin;

[NonParallelizable] // adds user2 to the tenant, which UserManagerTests and RoleManagerTests remove
[TestFixture(Description = "Change lead applicant")]
public sealed class ChangeLeadApplicantTests : PlaywrightTestBase
{
    private readonly string _newLeadApplicantEmail = AuthUsers.ResolveAuthUser("user2").Email;

    [OneTimeSetUp]
    public async Task AddNewLeadApplicantToTenantAsync() =>
        await Users.AddUserToRoleAsync(AdminApiClient, _newLeadApplicantEmail, TestData.User2DisplayName,
            TestData.UserRoleName, TestData.DefaultTemplateName);

    [TestCase(TestName = "Admin can change the lead applicant, and only the new lead applicant can open and submit it")]
    [CiRetry]
    public async Task AdminCanChangeTheLeadApplicantAsync()
    {
        var application = await CreateApplicationAsync();
        var viewApplications = new ViewApplications(Page);
        var dashboardPage = new DashboardPage(Page);
        var taskList = new TaskListPage(Page);
        var preview = new ApplicationPreviewPage(Page);

        await OpenApplicationsAsAdminAsync(viewApplications);
        await viewApplications.ChangeLeadApplicantAsync(application.ApplicationReference, _newLeadApplicantEmail);

        // The message says more when the new lead applicant also had to be given access to the template.
        await viewApplications.ExpectSuccessAsync("Lead applicant updated.");
        await viewApplications.SummaryCards
            .WithReference(application.ApplicationReference)
            .HasSummaryValue("Lead applicant", _newLeadApplicantEmail)
            .VerifyAsync();

        await LoginAsync("user2");
        await dashboardPage.GoToAsync();
        await dashboardPage.ExpectApplicationPresentAsync(application.ApplicationReference);
        await taskList.GoToAsync(application.ApplicationReference);
        await taskList.ReviewApplicationAsync();
        await preview.ExpectLeadApplicantCanSubmitAsync(Terminology.Singular);

        // The previous lead applicant's access to the application is taken away.
        await LoginAsync("default");
        await dashboardPage.GoToAsync();
        await dashboardPage.ExpectApplicationNotPresentAsync(application.ApplicationReference);
        await taskList.GoToAsync(application.ApplicationReference);
        await new ErrorPage(Page).ExpectPageNotFoundAsync();

        // Submitting needs Write on the application and to be its creator, which changing the lead applicant
        // must both hand over. Done through the API: it doesn't need every task complete, unlike the web journey.
        // NUnit 5's async asserts return a Task: without await they don't assert anything.
        var previousLeadSubmit = await Assert.ThrowsAsync<ApiRequestException>(() =>
            ApplicationApi.SubmitApplicationAsync(ApiClient, application.ApplicationId));
        Assert.That(previousLeadSubmit!.Message, Does.Contain("(403)"));

        await using var newLeadApiClient = await ApiBase.CreateApiRequestContextAsync(
            AssemblySetup.Playwright, ApiConfigFactory.ApiConfigForUser("user2", ApiConfig));
        await Assert.DoesNotThrowAsync(() =>
            ApplicationApi.SubmitApplicationAsync(newLeadApiClient, application.ApplicationId));
    }

    [OneTimeTearDown]
    public async Task RemoveNewLeadApplicantFromTenantAsync() =>
        await Users.RemoveUserFromTenantAsync(AdminApiClient, _newLeadApplicantEmail);

    // Created as the default user, so they start as its lead applicant.
    private Task<CreateApplicationResponse> CreateApplicationAsync() =>
        ApplicationApi.CreateApplicationAsync(ApiClient, ApplicationBuilder.CreateApplicationRequest(ApiConfig.TemplateId));

    private async Task OpenApplicationsAsAdminAsync(ViewApplications viewApplications)
    {
        await LoginAsync("admin");
        await viewApplications.GoToAsync();
        await viewApplications.SelectATemplateAsync(TestData.DefaultTemplateName);
    }
}
