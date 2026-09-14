import { test } from '../../fixtures/test';
import { login } from '../../support/login';
import { DashboardPage } from '../../pages/DashboardPage';
import { ContributorsPage } from '../../pages/ContributorsPage';
import { TaskListPage } from '../../pages/TaskListPage';
import { ApplicationPreviewPage } from '../../pages/ApplicationPreviewPage';
import {
  DeclarationPage,
  DetailsOfAcademiesPage,
  FinanceAndOperationsPage,
  GovernanceStructurePage,
  HighQualityInclusiveEducationPage,
  IncomingTrustPage,
  LeadershipPage,
  MembersPage,
  OutgoingTrustPage,
  ReasonsAndBenefitsIncomingPage,
  ReasonsAndBenefitsOutgoingPage,
  RisksPage,
  SchoolImprovementPage,
  TrusteesPage,
} from '../../pages/transfers';

const data = {
  incomingTrust: 'CANONS HIGH SCHOOL',
  academy: 'St Marys C of E Primary and Nursery, Academy, Handsworth',
  outgoingTrust: 'CANONIUM LEARNING TRUST',
};

test.describe('Transfers create and submit', () => {
  // test.setTimeout(360000);

  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('create and submit an application', async ({ page, terminology }) => {
    const dashboardPage = new DashboardPage(page, terminology);
    const contributorsPage = new ContributorsPage(page, terminology);

    await dashboardPage.startNewApplication();
    await contributorsPage.proceedToForm();

    const taskList = new TaskListPage(page);
    await taskList.expectLoaded();

    // About the trust that academies are joining
    const incomingTrust = new IncomingTrustPage(page);
    await incomingTrust.open();
    await incomingTrust.complete(data.incomingTrust);
    await incomingTrust.expectCompleted();

    const reasonsIncoming = new ReasonsAndBenefitsIncomingPage(page);
    await reasonsIncoming.open();
    await reasonsIncoming.complete();
    await reasonsIncoming.expectCompleted();

    const hqie = new HighQualityInclusiveEducationPage(page);
    await hqie.open();
    await hqie.complete();
    await hqie.expectCompleted();

    const schoolImprovement = new SchoolImprovementPage(page);
    await schoolImprovement.open();
    await schoolImprovement.complete();
    await schoolImprovement.expectCompleted();

    const finance = new FinanceAndOperationsPage(page);
    await finance.open();
    await finance.complete();
    await finance.expectCompleted();

    const leadership = new LeadershipPage(page);
    await leadership.open();
    await leadership.complete();
    await leadership.expectCompleted();

    const members = new MembersPage(page);
    await members.open();
    await members.addExistingMember('John Smith');
    await members.addNewMember('Alice Johnson', 'Past role description testing text');
    await members.addNewMember('Bob Brown', 'Past role description for Bob Brown');
    await members.addLeavingMember('Sarah White');
    await members.complete();
    await members.expectCompleted();

    const trustees = new TrusteesPage(page);
    await trustees.open();
    await trustees.addExistingTrustee('Michael Scott', 'Granting Officer', true);
    await trustees.addNewTrustee('Pam Beesly', 'Past role description', true, 'Future role description');
    await trustees.addNewTrustee(
      'Jim Halpert',
      'Past role description for Jim Halpert',
      false,
      'Future role description for Jim Halpert',
    );
    await trustees.addLeavingTrustee('Dwight Schrute');
    await trustees.complete();
    await trustees.expectCompleted();

    const governance = new GovernanceStructurePage(page);
    await governance.open();
    await governance.complete();
    await governance.expectCompleted();

    // About transferring academies
    const academies = new DetailsOfAcademiesPage(page);
    await academies.open();
    await academies.complete(data.academy);
    await academies.expectCompleted();

    const reasonsOutgoing = new ReasonsAndBenefitsOutgoingPage(page);
    await reasonsOutgoing.open();
    await reasonsOutgoing.complete();
    await reasonsOutgoing.expectCompleted();

    const risks = new RisksPage(page);
    await risks.open();
    await risks.complete();
    await risks.expectCompleted();

    // About the trusts that academies are leaving
    const outgoingTrust = new OutgoingTrustPage(page);
    await outgoingTrust.open();
    await outgoingTrust.complete(data.outgoingTrust);
    await outgoingTrust.expectCompleted();

    // Declaration
    const declaration = new DeclarationPage(page);
    await declaration.open();
    await declaration.complete();
    await declaration.expectCompleted();

    // Review and submit
    const preview = new ApplicationPreviewPage(page);
    await taskList.reviewApplication();
    await preview.submit();
    await preview.expectSubmitted();
  });
});
