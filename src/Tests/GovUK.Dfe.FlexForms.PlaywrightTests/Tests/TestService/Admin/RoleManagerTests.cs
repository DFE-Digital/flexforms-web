using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

public sealed class RoleManagerTests : PlaywrightTestBase
{
    private const string DefaultFormName = "default";
    private const string NewUserDisplayName = "Test Automation User 2";
    private const string CustomRoleName = "User Manager";

    [TestCase(TestName = "Admin can create a custom role and assign to a user")]
    [CiRetry]
    public async Task AdminCanCreateCustomRoleAndAssignToUserAsync()
    {
        var userToAddEmail = TestEnvironment.RequireEnvironmentVariable("USER2_EMAIL");
        await Users.AddUserToRoleAsync(AdminApiClient, userToAddEmail, NewUserDisplayName, "User", DefaultFormName);

        var adminPage = new AdminPage(Page, Terminology);
        var roleManager = new RoleManager(Page, Terminology);
        var managePermissions = new ManagePermissions(Page, Terminology);
        var userManager = new UserManager(Page, Terminology);
        var dashboardPage = new DashboardPage(Page, Terminology);

        await LoginAsync("admin");
        await Page.GotoAsync("/admin");
        await adminPage.OpenRoleManagerAsync();

        await roleManager.CreateRoleAsync(CustomRoleName);
        await roleManager.ManageRolePermissionsAsync(CustomRoleName);

        await managePermissions.AddPermissionAsync("User", "Any", "Manage");

        await Page.GotoAsync("/admin/user-manager");
        await userManager.EditUserAsync(userToAddEmail, newRole: CustomRoleName);

        await LoginAsync("user2");
        await dashboardPage.GoToAsync(NavigationSection.Admin);
        await adminPage.OpenUserManagerAsync();
    }

    [TestCase(TestName = "Admin can remove a custom role from a user and delete the role")]
    [CiRetry]
    public async Task AdminCanRemoveCustomRoleFromUserAndDeleteRoleAsync()
    {
        var userToRemoveEmail = TestEnvironment.RequireEnvironmentVariable("USER2_EMAIL");

        await Roles.CreateRoleAsync(AdminApiClient, CustomRoleName);
        await Users.AddUserToRoleAsync(AdminApiClient, userToRemoveEmail, NewUserDisplayName, CustomRoleName,
            DefaultFormName);

        var userManager = new UserManager(Page, Terminology);
        var roleManager = new RoleManager(Page, Terminology);

        await LoginAsync("admin");

        await Page.GotoAsync("/admin/user-manager");
        await userManager.EditUserAsync(userToRemoveEmail, newRole: "User");

        await Page.GotoAsync("/admin/role-manager");
        await roleManager.DeleteRoleAsync(CustomRoleName);
    }

    [TearDown]
    public async Task RemoveUserFromTenantAsync()
    {
        var userEmail = TestEnvironment.RequireEnvironmentVariable("USER2_EMAIL");
        
        // workaround for bug 306990
        await Users.AddUserToRoleAsync(AdminApiClient, userEmail, NewUserDisplayName, "User", DefaultFormName);
        
        await Users.RemoveUserFromTenantAsync(AdminApiClient, userEmail);
        await Roles.RemoveRoleAsync(AdminApiClient, CustomRoleName);
    }
}