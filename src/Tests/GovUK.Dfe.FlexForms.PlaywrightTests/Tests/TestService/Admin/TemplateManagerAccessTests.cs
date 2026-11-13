using System.Text.RegularExpressions;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

/// <summary>
/// Template managers (a custom role with Template:Any:Manage) see every template, but can only work in
/// the ones they have been given. The others are greyed out, and the server turns them away too.
/// </summary>
[NonParallelizable] // changes user2's role, which UserManagerTests and RoleManagerTests also use
[TestFixture(Description = "Template manager access")]
public sealed class TemplateManagerAccessTests : PlaywrightTestBase
{
    private const string TemplateManagerRoleName = "Template Manager Test";
    private const string GivenTemplateName = TestData.DefaultTemplateName;
    private const string OtherTemplateName = TestData.LiveSwitchOnlyTemplateName;

    private readonly string _templateManagerEmail = AuthUsers.ResolveAuthUser("user2").Email;
    private string _givenTemplateId = null!;
    private string _otherTemplateId = null!;

    [OneTimeSetUp]
    public async Task MakeUser2ATemplateManagerForOneTemplateAsync()
    {
        await RemoveTemplateManagerAsync();

        var role = await Roles.CreateRoleAsync(AdminApiClient, TemplateManagerRoleName);
        await Roles.SetPermissionsAsync(AdminApiClient, role.RoleId,
            new RolePermissionGrant("Template", "Any", "Manage"));

        // A custom role assigned with templates also gives Read and Write on those templates.
        await Users.AddUserToRoleAsync(AdminApiClient, _templateManagerEmail, TestData.User2DisplayName,
            TemplateManagerRoleName, GivenTemplateName);

        // A not-live template shows a preview banner when selected, which the link test relies on.
        await Templates.UpdateTemplateLiveAsync(AdminApiClient, OtherTemplateName, false);

        _givenTemplateId = await Templates.GetTemplateIdByNameAsync(AdminApiClient, GivenTemplateName);
        _otherTemplateId = await Templates.GetTemplateIdByNameAsync(AdminApiClient, OtherTemplateName);
    }

    [SetUp]
    public async Task LoginAsTemplateManagerAsync() => await LoginAsync("user2");

    [TestCase(TestName = "A template manager can't use a template they haven't been given on the Admin page")]
    [CiRetry]
    public async Task AdminPageGreysOutTemplatesWithoutAccessAsync()
    {
        var adminPage = new AdminPage(Page);

        await adminPage.GoToAsync();
        await adminPage.ExpectTemplateAccessAsync(GivenTemplateName);
        await adminPage.ExpectNoTemplateAccessAsync(OtherTemplateName);

        await adminPage.OpenTemplateByIdAsync(GivenTemplateName, _otherTemplateId);
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/admin$"));
        await adminPage.ExpectErrorAsync(AdminPage.NoTemplateAccessText);
    }

    [TestCase(TestName = "A template manager can't choose a template they haven't been given on Choose a form")]
    [CiRetry]
    public async Task ChooseAFormGreysOutTemplatesWithoutAccessAsync()
    {
        var chooseFormPage = new ChooseFormPage(Page);

        await chooseFormPage.GoToAsync();
        await chooseFormPage.ExpectFormCanBeChosenAsync(_givenTemplateId);
        await chooseFormPage.ExpectFormCannotBeChosenAsync(_otherTemplateId);

        await chooseFormPage.ForceChooseFormAsync(_otherTemplateId);
        await chooseFormPage.ExpectErrorAsync("You do not have access to that form.");
    }

    [TestCase(TestName = "A template manager can't open a template they haven't been given from a link")]
    [CiRetry]
    public async Task TemplateIdInALinkIsIgnoredWithoutAccessAsync()
    {
        var dashboardPage = new DashboardPage(Page);

        await dashboardPage.GoToWithTemplateAsync(_otherTemplateId);

        // They stay on the template they were given, so there is no preview of the other one.
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/applications/dashboard"));
        await dashboardPage.ExpectPreviewBannerHiddenAsync(OtherTemplateName);
    }

    [OneTimeTearDown]
    public async Task RemoveTemplateManagerRoleAsync() => await RemoveTemplateManagerAsync();

    private async Task RemoveTemplateManagerAsync()
    {
        // workaround for bug 306990, as in RoleManagerTests
        await Users.AddUserToRoleAsync(AdminApiClient, _templateManagerEmail, TestData.User2DisplayName,
            TestData.UserRoleName, TestData.DefaultTemplateName);

        await Users.RemoveUserFromTenantAsync(AdminApiClient, _templateManagerEmail);
        await Roles.RemoveRoleAsync(AdminApiClient, TemplateManagerRoleName);
    }
}
