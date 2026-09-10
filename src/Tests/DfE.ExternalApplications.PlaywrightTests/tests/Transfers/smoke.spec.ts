import { test } from '../../fixtures/test';
import { login } from '../../support/login';
import { ApplicationBuilder } from '../../api/builders/applicationBuilder';
import { createApplication } from '../../api/application';
import type { CreateApplicationResponse } from '../../api/types';
import { DashboardPage } from '../../pages/DashboardPage';
import { ContributorsPage } from '../../pages/ContributorsPage';
import { ContributorsInvitePage } from '../../pages/ContributorsInvitePage';
import { formatApplicationDisplayDate } from '../../pages/components/ApplicationsTable';
import { getTemplateCustomStatuses } from '../../api/templates';

const contributor = {
  name: 'Playwright Test',
  email: 'playwright@test.com',
};

let application: CreateApplicationResponse;
let expectedCreatedStatus: string;

test.describe('Transfers smoke', () => {
  test.beforeAll(async ({ apiClient, apiConfig }) => {
    const applicationRequest = ApplicationBuilder.createApplicationRequest(apiConfig.templateId);
    application = await createApplication(apiClient, applicationRequest);
    const customApplicationStatusResponse = await getTemplateCustomStatuses(apiClient, apiConfig.templateId);
    expectedCreatedStatus =
      customApplicationStatusResponse.find((status) => status.applicationStatus === 'Created')?.label ?? 'Created';
  });

  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('should add a contributor', async ({ page, terminology }) => {
    const dashboardPage = new DashboardPage(page, terminology);
    const contributorsPage = new ContributorsPage(page, terminology);
    const contributorsInvitePage = new ContributorsInvitePage(page, terminology);

    await dashboardPage.startNewApplication();
    await contributorsPage.addContributor();

    await contributorsInvitePage.fillInvite(contributor.name, contributor.email);
    await contributorsInvitePage.sendInvite();

    await contributorsPage.expectContributor(2, contributor.name, contributor.email);
  });

  test('should filter applications by reference number', async ({ page, terminology }) => {
    const dashboardPage = new DashboardPage(page, terminology);

    await dashboardPage.filterApplications();
    await dashboardPage.filterApplicationsByReference(application.applicationReference);
    await dashboardPage.applyFilters();

    await dashboardPage.applicationsTable
      .hasTableHeaders(['Reference number', 'Date started', 'Date submitted', 'Status', 'Action'])
      .hasNumberOfRows(1)
      .withReference(application.applicationReference)
      .columnHasValue('Reference number', application.applicationReference)
      .columnHasValue('Date started', formatApplicationDisplayDate())
      .columnHasValue('Date submitted', 'Not submitted')
      .columnHasValue('Status', expectedCreatedStatus)
      .columnHasValueWithLink(
        'Action',
        `Continue ${terminology.singular}`,
        `/applications/${application.applicationReference}`,
      );
  });
});
