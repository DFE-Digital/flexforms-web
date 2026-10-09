using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

[NonParallelizable]
[TestFixture(Description = "Role management")]
public sealed class RoleManagerTests : PlaywrightTestBase
{
    private const string CustomRoleName = "User Manager";
    private readonly string _user2Email = AuthUsers.ResolveAuthUser("user2").Email;

    [SetUp]
    public async Task AdminLoginAndNavigateToRoleManagerAsync()
    {
        await LoginAsync("admin");
        var adminPage = new AdminPage(Page);
        await adminPage.GoToAsync();
        await adminPage.OpenRoleManagerAsync();
    }

    [TestCase(TestName = "Admin can create a custom role and assign to a user")]
    [CiRetry]
    public async Task AdminCanCreateCustomRoleAndAssignToUserAsync()
    {
        await Users.AddUserToRoleAsync(AdminApiClient, _user2Email, TestData.User2DisplayName,
            TestData.UserRoleName, TestData.DefaultTemplateName);

        var adminPage = new AdminPage(Page);
        var roleManager = new RoleManager(Page);
        var managePermissions = new ManagePermissions(Page);
        var userManager = new UserManager(Page);
        var dashboardPage = new DashboardPage(Page);

        await roleManager.CreateRoleAsync(CustomRoleName);
        await roleManager.ManageRolePermissionsAsync(CustomRoleName);

        await managePermissions.AddPermissionAsync("User", "Any", "Manage");

        await userManager.GoToAsync();
        await userManager.EditUserAsync(_user2Email, newRole: CustomRoleName);

        await LoginAsync("user2");
        await dashboardPage.NavigateToAsync(NavigationSection.Admin);
        await adminPage.OpenUserManagerAsync();
        await userManager.ExpectHeading("Access audit trail");
    }

    [TestCase(TestName = "Admin can remove a custom role from a user and delete the role")]
    [CiRetry]
    public async Task AdminCanRemoveCustomRoleFromUserAndDeleteRoleAsync()
    {
        await Roles.CreateRoleAsync(AdminApiClient, CustomRoleName);
        await Users.AddUserToRoleAsync(AdminApiClient, _user2Email, TestData.User2DisplayName, CustomRoleName,
            TestData.DefaultTemplateName);

        var userManager = new UserManager(Page);
        var roleManager = new RoleManager(Page);

        await userManager.GoToAsync();
        await userManager.EditUserAsync(_user2Email, newRole: TestData.UserRoleName);

        await roleManager.GoToAsync();
        await roleManager.DeleteRoleAsync(CustomRoleName);
    }

    [TearDown]
    public async Task RemoveUserFromTenantAsync()
    {
        // workaround for bug 306990
        await Users.AddUserToRoleAsync(AdminApiClient, _user2Email, TestData.User2DisplayName,
            TestData.UserRoleName, TestData.DefaultTemplateName);

        await Users.RemoveUserFromTenantAsync(AdminApiClient, _user2Email);
        await Roles.RemoveRoleAsync(AdminApiClient, CustomRoleName);
    }
}