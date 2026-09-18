import { test } from '../../fixtures/test';
import { login } from '../../support/login';
import { DashboardPage } from '../../pages/DashboardPage';
import { ContributorsPage } from '../../pages/ContributorsPage';
import { TaskListPage } from '../../pages/TaskListPage';
import { BeforeYouUploadTask } from '../../pages/lsrp/BeforeYouUploadTask';
import { UploadYourLSRPDataTemplateTask } from '../../pages/lsrp/UploadYourLSRPDataTemplateTask';
import { ApplicationPreviewPage } from '../../pages/ApplicationPreviewPage';
import { validateValidFileForApplication } from '../../api/files';

test.describe('LSRP create and submit', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('create and submit an application', async ({ page, terminology, apiClient }) => {
    const dashboardPage = new DashboardPage(page, terminology);
    const contributorsPage = new ContributorsPage(page, terminology);

    await dashboardPage.startNewApplication();
    await contributorsPage.proceedToForm();

    const taskList = new TaskListPage(page);
    await taskList.expectLoaded();

    const beforeYouUploadTask = new BeforeYouUploadTask(page);
    await beforeYouUploadTask.open();
    await beforeYouUploadTask.complete('Derbyshire');
    await beforeYouUploadTask.expectCompleted();

    const uploadYourLSRPDataTemplateTask = new UploadYourLSRPDataTemplateTask(page);
    await uploadYourLSRPDataTemplateTask.open();
    await uploadYourLSRPDataTemplateTask.complete();
    await uploadYourLSRPDataTemplateTask.expectCompleted();

    await validateValidFileForApplication(apiClient, page);

    const preview = new ApplicationPreviewPage(page);
    await taskList.reviewApplication();
    await preview.submit();
    await preview.expectSubmitted();
  });
});
