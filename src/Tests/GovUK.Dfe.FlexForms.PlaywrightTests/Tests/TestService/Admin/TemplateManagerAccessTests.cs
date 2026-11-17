using System.Text.RegularExpressions;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;
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

    private const string GivenLiveTemplateName = TestData.DefaultTemplateName;
    private const string GivenTemplateName = TestData.TemplateStudioTemplateName;

    private const string OtherTemplateName = TestData.TemplateManagerNoAccessTemplateName;

    private readonly string _templateManagerEmail = AuthUsers.ResolveAuthUser("user2").Email;
    private string _givenLiveTemplateId = null!;
    private string _givenTemplateId = null!;
    private string _otherTemplateId = null!;

    [OneTimeSetUp]
    public async Task MakeUser2ATemplateManagerForTwoTemplatesAsync()
    {
        await RemoveTemplateManagerAsync();

        var givenTemplate = await FindOrCreateTemplateAsync(GivenTemplateName);
        // It doesn't need a version: the tests only check it is greyed out and turned away.
        var otherTemplate = await FindOrCreateTemplateAsync(OtherTemplateName);

        var role = await Roles.CreateRoleAsync(AdminApiClient, TemplateManagerRoleName);
        await Roles.SetPermissionsAsync(AdminApiClient, role.RoleId,
            new RolePermissionGrant("Template", "Any", "Manage"));

        // A custom role assigned with templates also gives Read and Write on those templates.
        await Users.AddUserToRoleAsync(AdminApiClient, _templateManagerEmail, TestData.User2DisplayName,
            TemplateManagerRoleName, GivenLiveTemplateName, GivenTemplateName);

        _givenLiveTemplateId = await Templates.GetTemplateIdByNameAsync(AdminApiClient, GivenLiveTemplateName);
        _givenTemplateId = givenTemplate.TemplateId;
        _otherTemplateId = otherTemplate.TemplateId;
    }

    [SetUp]
    public async Task LoginAsTemplateManagerAsync() => await LoginAsync("user2");

    [TestCase(TestName = "A template manager sees the template sections of Admin, and can open only the templates they've been given")]
    [CiRetry]
    public async Task AdminPageOffersOnlyTheGivenTemplatesAsync()
    {
        var adminPage = new AdminPage(Page);
        var dashboardPage = new DashboardPage(Page);

        await adminPage.GoToAsync();

        // The role gives the template sections and nothing else.
        await adminPage.ExpectSectionAsync("Tenant templates");
        await adminPage.ExpectSectionAsync("Template Management");
        await adminPage.ExpectNoSectionAsync("Users & Roles");
        await adminPage.ExpectNoSectionAsync("Applications");
        await adminPage.ExpectNoSectionAsync("Tenant Admin");

        await adminPage.ExpectTemplateAccessAsync(GivenLiveTemplateName);
        await adminPage.ExpectTemplateAccessAsync(GivenTemplateName);
        await adminPage.ExpectNoTemplateAccessAsync(OtherTemplateName);

        await adminPage.OpenTemplateByIdAsync(GivenTemplateName, _otherTemplateId);
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/admin$"));
        await adminPage.ExpectErrorAsync(AdminPage.NoTemplateAccessText);

        // A given template that isn't live opens as a preview.
        await adminPage.OpenTemplateAsync(GivenTemplateName);
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/applications/dashboard"));
        await dashboardPage.ExpectPreviewBannerVisibleAsync(GivenTemplateName);
    }

    [TestCase(TestName = "A template manager can choose only the templates they've been given, on Choose a form or from a link")]
    [CiRetry]
    public async Task OnlyTheGivenTemplatesCanBeChosenAsync()
    {
        var chooseFormPage = new ChooseFormPage(Page);
        var dashboardPage = new DashboardPage(Page);

        await chooseFormPage.GoToAsync();
        await chooseFormPage.ExpectFormCanBeChosenAsync(_givenLiveTemplateId);
        await chooseFormPage.ExpectFormCanBeChosenAsync(_givenTemplateId);
        await chooseFormPage.ExpectFormCannotBeChosenAsync(_otherTemplateId);

        // Unlike other users, a template manager can choose a given template that isn't live, as a preview.
        await chooseFormPage.SelectFormAsync(GivenTemplateName, "Not live");
        await chooseFormPage.GoToDashboardAsync();
        await dashboardPage.ExpectPreviewBannerVisibleAsync(GivenTemplateName);

        await dashboardPage.GoToWithTemplateAsync(_otherTemplateId);
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/applications/dashboard"));
        await dashboardPage.ExpectPreviewBannerVisibleAsync(GivenTemplateName);
        await dashboardPage.ExpectPreviewBannerHiddenAsync(OtherTemplateName);
    }

    [TestCase(TestName = "A template manager can save a new version of a template they've been given in Template Studio, and can't choose others")]
    [CiRetry]
    public async Task TemplateStudioSavesOnlyTheGivenTemplatesAsync()
    {
        var studio = new TemplateStudio(Page);

        await studio.OpenAsync(_givenTemplateId);
        await studio.ExpectOpenAsync(GivenTemplateName);
        await studio.ExpectTemplateCanBeChosenAsync(_givenLiveTemplateId);
        await studio.ExpectTemplateCannotBeChosenAsync(_otherTemplateId);

        var version = await studio.SaveNewVersionAsync();
        await studio.ExpectSavedAsync(version);

        var saved = await Templates.FindTemplateByNameAsync(AdminApiClient, GivenTemplateName);
        Assert.That(saved?.LatestVersionNumber, Is.EqualTo(version));
    }

    [OneTimeTearDown]
    public async Task RemoveTemplateManagerRoleAsync() => await RemoveTemplateManagerAsync();

    private async Task<TemplateDto> FindOrCreateTemplateAsync(string name) =>
        await Templates.FindTemplateByNameAsync(AdminApiClient, name)
        ?? await Templates.CreateTemplateAsync(AdminApiClient, name);

    private async Task RemoveTemplateManagerAsync()
    {
        // workaround for bug 306990, as in RoleManagerTests
        await Users.AddUserToRoleAsync(AdminApiClient, _templateManagerEmail, TestData.User2DisplayName,
            TestData.UserRoleName, TestData.DefaultTemplateName);

        await Users.RemoveUserFromTenantAsync(AdminApiClient, _templateManagerEmail);
        await Roles.RemoveRoleAsync(AdminApiClient, TemplateManagerRoleName);
    }
}
