using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;
using NUnit.Framework;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService;

[TestFixture(Description = "Applications")]
public sealed class ApplicationsTests : PlaywrightTestBase
{
    private static readonly ConfiguredFieldsData Data = new(
        Academy: "Testbourne Community School",
        LocalAuthority: "Sheffield",
        Diocese: "Diocese of Sheffield",
        Mp: "Olivia");

    private const string Trust = "5 DIMENSIONS TRUST";

    protected override int TestTimeoutMilliseconds => 180_000;

    [SetUp]
    public async Task LoginAsDefaultUserAsync() => await LoginAsync();

    [TestCase(TestName = "Complete application")]
    [CiRetry]
    public async Task CompleteApplicationAsync()
    {
        var dashboardPage = new DashboardPage(Page, Terminology);
        var contributorsPage = new ContributorsPage(Page, Terminology);

        await dashboardPage.StartNewApplicationAsync();
        await contributorsPage.ProceedToFormAsync();

        var standardFieldsTask = new StandardFieldsTask(Page);
        await standardFieldsTask.OpenAsync();
        await standardFieldsTask.CompleteAsync();
        await standardFieldsTask.ExpectCompletedAsync();

        var configuredFieldsTask = new ConfiguredFieldsTask(Page);
        await configuredFieldsTask.OpenAsync();
        await configuredFieldsTask.CompleteAsync(Data);
        await configuredFieldsTask.ExpectCompletedAsync();

        await Files.ValidateValidFileForApplicationAsync(ApiClient, Page);

        var trustDetailsTask = new TrustDetailsTask(Page);
        await trustDetailsTask.OpenAsync();
        await trustDetailsTask.CompleteAsync(Trust);
        await trustDetailsTask.ExpectCompletedAsync();

        var addSampleItemsTask = new AddSampleItemsTask(Page);
        await addSampleItemsTask.OpenAsync();
        await addSampleItemsTask.CompleteAsync(Data.Academy);
        await addSampleItemsTask.ExpectCompletedAsync();

        var signItemDeclarationsTask = new SignItemDeclarationsTask(Page);
        await signItemDeclarationsTask.OpenAsync();
        await signItemDeclarationsTask.CompleteAsync(Data.Academy);
        await signItemDeclarationsTask.ExpectCompletedAsync();

        var taskList = new TaskListPage(Page);
        await taskList.ReviewApplicationAsync();

        var preview = new ApplicationPreviewPage(Page);
        await preview.SubmitAsync();
        await preview.ExpectSubmittedAsync();
    }
}
