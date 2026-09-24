using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;
using NUnit.Framework;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.Transfers;

[TestFixture(Description = "Transfers create and submit")]
[Category("Transfers")]
public sealed class CreateAndSubmitTests : PlaywrightTestBase
{
    private const string IncomingTrustName = "CANONS HIGH SCHOOL";
    private const string Academy = "St Marys C of E Primary and Nursery, Academy, Handsworth";
    private const string OutgoingTrustName = "CANONIUM LEARNING TRUST";

    protected override int TestTimeoutMilliseconds => 600_000;

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

        // About the trust that academies are joining
        var incomingTrust = new IncomingTrustPage(Page);
        await incomingTrust.OpenAsync();
        await incomingTrust.CompleteAsync(IncomingTrustName);
        await incomingTrust.ExpectCompletedAsync();

        var reasonsIncoming = new ReasonsAndBenefitsIncomingPage(Page);
        await reasonsIncoming.OpenAsync();
        await reasonsIncoming.CompleteAsync();
        await reasonsIncoming.ExpectCompletedAsync();

        var hqie = new HighQualityInclusiveEducationPage(Page);
        await hqie.OpenAsync();
        await hqie.CompleteAsync();
        await hqie.ExpectCompletedAsync();

        var schoolImprovement = new SchoolImprovementPage(Page);
        await schoolImprovement.OpenAsync();
        await schoolImprovement.CompleteAsync();
        await schoolImprovement.ExpectCompletedAsync();

        var finance = new FinanceAndOperationsPage(Page);
        await finance.OpenAsync();
        await finance.CompleteAsync();
        await finance.ExpectCompletedAsync();

        var leadership = new LeadershipPage(Page);
        await leadership.OpenAsync();
        await leadership.CompleteAsync();
        await leadership.ExpectCompletedAsync();

        var members = new MembersPage(Page);
        await members.OpenAsync();
        await members.AddExistingMemberAsync("John Smith");
        await members.AddNewMemberAsync("Alice Johnson", "Past role description testing text");
        await members.AddNewMemberAsync("Bob Brown", "Past role description for Bob Brown");
        await members.AddLeavingMemberAsync("Sarah White");
        await members.CompleteAsync();
        await members.ExpectCompletedAsync();

        var trustees = new TrusteesPage(Page);
        await trustees.OpenAsync();
        await trustees.AddExistingTrusteeAsync("Michael Scott", "Granting Officer", true);
        await trustees.AddNewTrusteeAsync("Pam Beesly", "Past role description", true, "Future role description");
        await trustees.AddNewTrusteeAsync(
            "Jim Halpert",
            "Past role description for Jim Halpert",
            false,
            "Future role description for Jim Halpert");
        await trustees.AddLeavingTrusteeAsync("Dwight Schrute");
        await trustees.CompleteAsync();
        await trustees.ExpectCompletedAsync();

        var governance = new GovernanceStructurePage(Page);
        await governance.OpenAsync();
        await governance.CompleteAsync();
        await governance.ExpectCompletedAsync();

        // About transferring academies
        var academies = new DetailsOfAcademiesPage(Page);
        await academies.OpenAsync();
        await academies.CompleteAsync(Academy);
        await academies.ExpectCompletedAsync();

        var reasonsOutgoing = new ReasonsAndBenefitsOutgoingPage(Page);
        await reasonsOutgoing.OpenAsync();
        await reasonsOutgoing.CompleteAsync();
        await reasonsOutgoing.ExpectCompletedAsync();

        var risks = new RisksPage(Page);
        await risks.OpenAsync();
        await risks.CompleteAsync();
        await risks.ExpectCompletedAsync();

        // About the trusts that academies are leaving
        var outgoingTrust = new OutgoingTrustPage(Page);
        await outgoingTrust.OpenAsync();
        await outgoingTrust.CompleteAsync(OutgoingTrustName);
        await outgoingTrust.ExpectCompletedAsync();

        // Declaration
        var declaration = new DeclarationPage(Page);
        await declaration.OpenAsync();
        await declaration.CompleteAsync();
        await declaration.ExpectCompletedAsync();

        // Review and submit
        var preview = new ApplicationPreviewPage(Page);
        await taskList.ReviewApplicationAsync();
        await preview.SubmitAsync();
        await preview.ExpectSubmittedAsync();
    }
}
