using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Lsrp;
using NUnit.Framework;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.Lsrp;

[TestFixture(Description = "LSRP create and submit")]
public sealed class SubmitApplicationTests : PlaywrightTestBase
{
    [SetUp]
    public async Task LoginAsDefaultUserAsync() => await LoginAsync();

    [TestCase(TestName = "create and submit an application")]
    [CiRetry]
    public async Task CreateAndSubmitAnApplicationAsync()
    {
        var dashboardPage = new DashboardPage(Page, Terminology);
        var contributorsPage = new ContributorsPage(Page, Terminology);

        await dashboardPage.StartNewApplicationAsync();
        await contributorsPage.ProceedToFormAsync();

        var taskList = new TaskListPage(Page);
        await taskList.ExpectLoadedAsync();

        var beforeYouUploadTask = new BeforeYouUploadTask(Page);
        await beforeYouUploadTask.OpenAsync();
        await beforeYouUploadTask.CompleteAsync("Derbyshire");
        await beforeYouUploadTask.ExpectCompletedAsync();

        var uploadYourLsrpDataTemplateTask = new UploadYourLSRPDataTemplateTask(Page);
        await uploadYourLsrpDataTemplateTask.OpenAsync();
        await uploadYourLsrpDataTemplateTask.CompleteAsync();
        await uploadYourLsrpDataTemplateTask.ExpectCompletedAsync();

        await Files.ValidateValidFileForApplicationAsync(ApiClient, Page);

        var preview = new ApplicationPreviewPage(Page);
        await taskList.ReviewApplicationAsync();
        await preview.SubmitAsync();
        await preview.ExpectSubmittedAsync();
    }
}
