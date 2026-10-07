using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

public sealed class UserManagerTests : PlaywrightTestBase
{
    private const string DefaultFormName = "default";
    private const string NewUserDisplayName = "Test Automation User 2";
    private const string NewUserRole = "User";
    private string _userToAddEmail = null!;

    [TestCase(TestName = "Admin can create user and give access to a form")]
    [CiRetry]
    public async Task AdminCanCreateUserAndGiveAccessToFormAsync()
    {
        _userToAddEmail = TestEnvironment.RequireEnvironmentVariable("USER2_EMAIL");
        await Users.RemoveUserFromTenantAsync(AdminApiClient, _userToAddEmail);

        var adminPage = new AdminPage(Page, Terminology);
        var userManager = new UserManager(Page, Terminology);
        var dashboardPage = new DashboardPage(Page, Terminology);
        var contributorsPage = new ContributorsPage(Page, Terminology);
        var standardFieldsTask = new StandardFieldsTask(Page);

        await LoginAsync("admin");
        await adminPage.GoToAsync();
        await adminPage.OpenUserManagerAsync();

        await userManager.OpenAddUserAsync();
        await userManager.AddUserAsync(NewUserDisplayName, _userToAddEmail, NewUserRole, [DefaultFormName]);

        await LoginAsync("user2");
        await dashboardPage.StartNewApplicationAsync();
        await contributorsPage.ProceedToFormAsync();
        await standardFieldsTask.OpenAsync();
        await standardFieldsTask.CompleteAsync();
        await standardFieldsTask.ExpectCompletedAsync();
    }

    [TestCase(TestName = "Admin can remove user from tenant")]
    [CiRetry]
    public async Task AdminCanRemoveUserFromTenantAsync()
    {
        _userToAddEmail = TestEnvironment.RequireEnvironmentVariable("USER2_EMAIL");
        await Users.AddUserToRoleAsync(AdminApiClient, _userToAddEmail, NewUserDisplayName, NewUserRole,
            DefaultFormName);

        var userManager = new UserManager(Page, Terminology);
        var dashboardPage = new DashboardPage(Page, Terminology);

        await LoginAsync("admin");
        await userManager.GoToAsync();

        await userManager.RemoveUserFromTenantAsync(_userToAddEmail);

        await SignInAsync("user2");
        await dashboardPage.GoToAsync();

        await new ErrorPage(Page, Terminology).ExpectInvalidOrExpiredTokensAsync();
    }

    [OneTimeTearDown]
    public async Task RemoveUserFromTenantAsync()
    {
        await Users.RemoveUserFromTenantAsync(AdminApiClient, _userToAddEmail);
    }
}