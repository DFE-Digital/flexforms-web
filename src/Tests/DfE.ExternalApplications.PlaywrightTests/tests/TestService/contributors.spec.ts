import { test } from '../../fixtures/test';
import { login } from '../../support/login';
import { ApplicationBuilder } from '../../api/builders/applicationBuilder';
import { createApplication } from '../../api/application';
import type { CreateApplicationResponse } from '../../api/types';
import { expect, type APIRequestContext } from '@playwright/test';
import { requireEnvironmentVariable } from '../../support/environment';
import { DashboardPage } from '../../pages/DashboardPage';
import { ContributorsPage } from '../../pages/ContributorsPage';
import { ContributorsInvitePage } from '../../pages/ContributorsInvitePage';
import { ApplicationPage } from '../../pages/ApplicationPage';
import { StandardFieldsTask } from '../../pages/tasks/StandardFieldsTask';
import { TaskListPage } from '../../pages/TaskListPage';
import { ApplicationPreviewPage } from '../../pages/ApplicationPreviewPage';

const contributor = {
  name: 'Test Automation User',
  email: requireEnvironmentVariable('DEFAULT_USER_EMAIL'),
};

async function createApplicationForTemplate(
  request: APIRequestContext,
  templateId: string,
): Promise<CreateApplicationResponse> {
  return createApplication(request, ApplicationBuilder.createApplicationRequest(templateId));
}

test.describe('Contributor permission tests', () => {
  test('caseworker can view any application, but not edit', async ({ page, apiClient, apiConfig }) => {
    const application = await createApplicationForTemplate(apiClient, apiConfig.templateId);
    const applicationUrl = `/applications/${application.applicationReference}`;

    await login(page, 'caseworker');
    await page.goto(applicationUrl);
    await expect(page).toHaveURL(applicationUrl);

    const standardFieldsTask = new StandardFieldsTask(page);
    await standardFieldsTask.unableToOpen();
    await page.goto(`${applicationUrl}/standard-fields/full-name-page`);
    await expect(page).toHaveURL(applicationUrl);
  });

  test('admin can edit any application', async ({ page, apiClient, apiConfig }) => {
    const application = await createApplicationForTemplate(apiClient, apiConfig.templateId);
    const applicationUrl = `/applications/${application.applicationReference}`;

    await login(page, 'admin');
    await page.goto(applicationUrl);
    await expect(page).toHaveURL(applicationUrl);

    const standardFieldsTask = new StandardFieldsTask(page);
    await standardFieldsTask.open();
    await standardFieldsTask.complete();
    await standardFieldsTask.expectCompleted();
  });

  test("admin cannot submit another user's application", async ({ page, terminology, apiClient, apiConfig }) => {
    const application = await createApplicationForTemplate(apiClient, apiConfig.templateId);

    await login(page, 'admin');
    await page.goto(`/applications/${application.applicationReference}`);

    const taskList = new TaskListPage(page);
    await taskList.reviewApplication();

    const preview = new ApplicationPreviewPage(page);
    await preview.expectNonLeadApplicantCannotSubmit(terminology.singular);
  });

  test('user should not be able to view an application that is not shared with them', async ({
    page,
    terminology,
    adminApiClient,
    apiConfig,
  }) => {
    const application = await createApplicationForTemplate(adminApiClient, apiConfig.templateId);
    const dashboardPage = new DashboardPage(page, terminology);

    await login(page);
    await page.goto('/');
    await dashboardPage.expectApplicationNotPresent(application.applicationReference);

    await page.goto(`/applications/${application.applicationReference}`);
    await expect(page).toHaveURL('Error/NotFound');
  });

  test('should be able to add a contributor and that contributor should be able to edit the application', async ({
    page,
    terminology,
    adminApiClient,
    apiConfig,
  }) => {
    const application = await createApplicationForTemplate(adminApiClient, apiConfig.templateId);
    const applicationPage = new ApplicationPage(page, terminology);
    const dashboardPage = new DashboardPage(page, terminology);
    const contributorsPage = new ContributorsPage(page, terminology);
    const contributorsInvitePage = new ContributorsInvitePage(page, terminology);

    await login(page, 'admin');
    await page.goto(`/applications/${application.applicationReference}`);

    await applicationPage.inviteContributors();

    await contributorsPage.addContributor();

    await contributorsInvitePage.fillInvite(contributor.name, contributor.email);
    await contributorsInvitePage.sendInvite();

    await contributorsPage.expectContributor(2, contributor.name, contributor.email);

    await login(page, 'default');
    await dashboardPage.expectApplicationPresent(application.applicationReference);
  });
});
