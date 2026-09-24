using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Visits;
using NUnit.Framework;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.Visits;

[TestFixture(Description = "Visit create and submit")]
[Category("Visits")]
public sealed class SubmitVisitTests : PlaywrightTestBase
{
    protected override int TestTimeoutMilliseconds => 180_000;

    [SetUp]
    public async Task LoginAsDefaultUserAsync() => await LoginAsync();

    [TestCase(TestName = "create and submit a visit")]
    [CiRetry]
    public async Task CreateAndSubmitAVisitAsync()
    {
        var dashboardPage = new DashboardPage(Page, Terminology);
        await dashboardPage.StartNewApplicationAsync();

        var taskList = new TaskListPage(Page);
        await taskList.ExpectLoadedAsync();

        var visitOrganisationTask = new VisitOrganisationTask(Page);
        await visitOrganisationTask.OpenAsync();
        await visitOrganisationTask.AddTrustAsync("5 Dimensions Trust");
        await visitOrganisationTask.AddSchoolAsync("St Marys C of E Primary and Nursery, Academy, Handsworth");
        await visitOrganisationTask.AddLocalAuthorityAsync("Sheffield");
        await visitOrganisationTask.AddDioceseAsync("Diocese of Sheffield");
        await visitOrganisationTask.AddOtherOrganisationAsync("Test Organisation");
        await visitOrganisationTask.AddDfEOrganisedConferenceAsync("Test DfE Organised Conference");
        await visitOrganisationTask.AddExternallyOrganisedConferenceAsync("Test Externally Organised Conference");
        await visitOrganisationTask.CompleteAsync();

        var dateOfVisitTask = new DateOfTheVisitTask(Page);
        await dateOfVisitTask.OpenAsync();
        await dateOfVisitTask.CompleteAsync();
        await dateOfVisitTask.ExpectCompletedAsync();

        var attendeesAtTheVisitTask = new AttendeesAtTheVisitTask(Page);
        await attendeesAtTheVisitTask.OpenAsync();
        await attendeesAtTheVisitTask.AddLeadAttendeeAsync("Lee Datten-dee", "Test Lead");
        await attendeesAtTheVisitTask.AddDfEAttendeeAsync("Duffy Edun", "Test DfE");
        await attendeesAtTheVisitTask.AddExternalOrganisationAttendeeAsync("Elsa Orr", "Test External");
        await attendeesAtTheVisitTask.CompleteAsync();
        await attendeesAtTheVisitTask.ExpectCompletedAsync();

        var conversationDetailsTask = new ConversationDetailsTask(Page);
        await conversationDetailsTask.OpenAsync();
        await conversationDetailsTask.CompleteAsync();
        await conversationDetailsTask.ExpectCompletedAsync();

        var preview = new ApplicationPreviewPage(Page);
        await taskList.ReviewApplicationAsync();
        await preview.SubmitAsync();
        await preview.ExpectSubmittedAsync("Visit record completed");
    }
}
