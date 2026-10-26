using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

// Nothing is saved, so the template stays as it is for other tests.
[TestFixture(Description = "Template Studio")]
public sealed class TemplateStudioTests : PlaywrightTestBase
{
    [SetUp]
    public async Task LoginAsAdminAsync() => await LoginAsync("admin");

    [TestCase(TestName = "Admin can open a template in Template Studio and check it for problems")]
    [CiRetry]
    public async Task AdminCanOpenAndCheckTemplateAsync()
    {
        var studio = new TemplateStudio(Page);

        await studio.OpenFromTemplateManagerAsync(TestData.DefaultTemplateName);
        await studio.CheckForProblemsAsync();
    }

    [TestCase(TestName = "Admin can preview an unsaved template and answer its first page")]
    [CiRetry]
    public async Task AdminCanPreviewUnsavedTemplateAsync()
    {
        var studio = new TemplateStudio(Page);
        await studio.OpenFromTemplateManagerAsync(TestData.DefaultTemplateName);

        var preview = await studio.OpenPreviewAsync();

        await Assertions.Expect(preview.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Task list" })).ToBeVisibleAsync();
        var start = preview.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save and continue" })
            .Or(preview.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Continue" }));
        await Assertions.Expect(start.First).ToBeVisibleAsync();
    }

    [TestCase(TestName = "Admin can build a rule with more than one condition")]
    [CiRetry]
    public async Task AdminCanBuildRuleWithConditionsAsync()
    {
        var studio = new TemplateStudio(Page);
        await studio.OpenFromTemplateManagerAsync(TestData.DefaultTemplateName);

        await studio.StartRuleAsync();
        await studio.AddConditionAsync();
    }
}
