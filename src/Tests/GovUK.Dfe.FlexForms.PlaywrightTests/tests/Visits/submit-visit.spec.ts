import { test } from '../../fixtures/test';
import { login } from '../../support/login';
import { DashboardPage } from '../../pages/DashboardPage';
import { TaskListPage } from '../../pages/TaskListPage';
import { VisitOrganisationTask } from '../../pages/visits/VisitOrganisationTask';
import { DateOfTheVisitTask } from '../../pages/visits/DateOfTheVisitTask';
import { AttendeesAtTheVisitTask } from '../../pages/visits/AttendeesAtTheVisitTask';
import { ConversationDetailsTask } from '../../pages/visits/ConversationDetailsTask';
import { ApplicationPreviewPage } from '../../pages/ApplicationPreviewPage';

test.describe('Visit create and submit', () => {
  test.describe.configure({ timeout: 180_000 });

  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('create and submit a visit', async ({ page, terminology }) => {
    const dashboardPage = new DashboardPage(page, terminology);
    await dashboardPage.startNewApplication();

    const taskList = new TaskListPage(page);
    await taskList.expectLoaded();

    const visitOrganisationTask = new VisitOrganisationTask(page);
    await visitOrganisationTask.open();
    await visitOrganisationTask.addTrust('5 Dimensions Trust');
    await visitOrganisationTask.addSchool('St Marys C of E Primary and Nursery, Academy, Handsworth');
    await visitOrganisationTask.addLocalAuthority('Sheffield');
    await visitOrganisationTask.addDiocese('Diocese of Sheffield');
    await visitOrganisationTask.addOtherOrganisation('Test Organisation');
    await visitOrganisationTask.addDfEOrganisedConference('Test DfE Organised Conference');
    await visitOrganisationTask.addExternallyOrganisedConference('Test Externally Organised Conference');
    await visitOrganisationTask.complete();

    const dateOfVisitTask = new DateOfTheVisitTask(page);
    await dateOfVisitTask.open();
    await dateOfVisitTask.complete();
    await dateOfVisitTask.expectCompleted();

    const attendeesAtTheVisitTask = new AttendeesAtTheVisitTask(page);
    await attendeesAtTheVisitTask.open();
    await attendeesAtTheVisitTask.addLeadAttendee('Lee Datten-dee', 'Test Lead');
    await attendeesAtTheVisitTask.addDfEAttendee('Duffy Edun', 'Test DfE');
    await attendeesAtTheVisitTask.addExternalOrganisationAttendee('Elsa Orr', 'Test External');
    await attendeesAtTheVisitTask.complete();
    await attendeesAtTheVisitTask.expectCompleted();

    const conversationDetailsTask = new ConversationDetailsTask(page);
    await conversationDetailsTask.open();
    await conversationDetailsTask.complete();
    await conversationDetailsTask.expectCompleted();

    const preview = new ApplicationPreviewPage(page);
    await taskList.reviewApplication();
    await preview.submit();
    await preview.expectSubmitted('Visit record completed');
  });
});
