import { test } from '../../fixtures/test';
import { login } from '../../support/login';
import { DashboardPage } from '../../pages/DashboardPage';
import { ContributorsPage } from '../../pages/ContributorsPage';
import { StandardFieldsTask } from '../../pages/tasks/StandardFieldsTask';
import { ConfiguredFieldsTask } from '../../pages/tasks/ConfiguredFieldsTask';
import { TrustDetailsTask } from '../../pages/tasks/TrustDetailsTask';
import { AddSampleItemsTask } from '../../pages/tasks/AddSampleItemsTask';
import { validateValidFileForApplication } from '../../api/files';
import { SignItemDeclarationsTask } from '../../pages/tasks/SignItemDeclarationsTask';
import { ApplicationPreviewPage } from '../../pages/ApplicationPreviewPage';
import { TaskListPage } from '../../pages/TaskListPage';

const data = {
  academy: 'Testbourne Community School',
  localAuthority: 'Sheffield',
  diocese: 'Diocese of Sheffield',
  trust: '5 DIMENSIONS TRUST',
  mp: 'Phillipson',
};

test.describe('Applications', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('Complete application', async ({ page, terminology, apiClient }) => {
    const dashboardPage = new DashboardPage(page, terminology);
    const contributorsPage = new ContributorsPage(page, terminology);

    await dashboardPage.startNewApplication();
    await contributorsPage.proceedToForm();

    const standardFieldsTask = new StandardFieldsTask(page);
    await standardFieldsTask.open();
    await standardFieldsTask.complete();
    await standardFieldsTask.expectCompleted();

    const configuredFieldsTask = new ConfiguredFieldsTask(page);
    await configuredFieldsTask.open();
    await configuredFieldsTask.complete(data);
    await configuredFieldsTask.expectCompleted();

    await validateValidFileForApplication(apiClient, page);

    const trustDetailsTask = new TrustDetailsTask(page);
    await trustDetailsTask.open();
    await trustDetailsTask.complete(data.trust);
    await trustDetailsTask.expectCompleted();

    const addSampleItemsTask = new AddSampleItemsTask(page);
    await addSampleItemsTask.open();
    await addSampleItemsTask.complete(data.academy);
    await addSampleItemsTask.expectCompleted();

    const signItemDeclarationsTask = new SignItemDeclarationsTask(page);
    await signItemDeclarationsTask.open();
    await signItemDeclarationsTask.complete(data.academy);
    await signItemDeclarationsTask.expectCompleted();

    const taskList = new TaskListPage(page);
    await taskList.reviewApplication();

    const preview = new ApplicationPreviewPage(page);
    await preview.submit();
    await preview.expectSubmitted();
  });
});
