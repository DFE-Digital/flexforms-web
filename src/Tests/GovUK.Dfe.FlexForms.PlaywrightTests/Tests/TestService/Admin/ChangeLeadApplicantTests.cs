using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

[NonParallelizable] // adds user2 to the tenant, which UserManagerTests and RoleManagerTests remove
[TestFixture(Description = "Change lead applicant")]
public sealed class ChangeLeadApplicantTests : PlaywrightTestBase
{
    private readonly string _leadApplicantEmail = AuthUsers.ResolveAuthUser("default").Email;
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
    }

    [TestCase(TestName = "Changing the lead applicant to the current lead applicant shows an error")]
    [CiRetry]
    public async Task ChangingToTheCurrentLeadApplicantShowsAnErrorAsync() =>
        await ExpectChangeRejectedAsync(_leadApplicantEmail, "This user is already the lead applicant.");

    [TestCase(TestName = "Changing the lead applicant to someone who is not in the tenant shows an error")]
    [CiRetry]
    public async Task ChangingToAUserNotInTheTenantShowsAnErrorAsync() =>
        await ExpectChangeRejectedAsync($"not-a-user-{Guid.NewGuid():N}@example.com",
            "That user was not found in this tenant.");

    [OneTimeTearDown]
    public async Task RemoveNewLeadApplicantFromTenantAsync() =>
        await Users.RemoveUserFromTenantAsync(AdminApiClient, _newLeadApplicantEmail);

    private async Task ExpectChangeRejectedAsync(string email, string error)
    {
        var application = await CreateApplicationAsync();
        var viewApplications = new ViewApplications(Page);

        await OpenApplicationsAsAdminAsync(viewApplications);
        await viewApplications.ChangeLeadApplicantAsync(application.ApplicationReference, email);

        await viewApplications.ExpectErrorAsync(error);
        await viewApplications.SummaryCards
            .WithReference(application.ApplicationReference)
            .HasSummaryValue("Lead applicant", _leadApplicantEmail)
            .VerifyAsync();
    }

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
