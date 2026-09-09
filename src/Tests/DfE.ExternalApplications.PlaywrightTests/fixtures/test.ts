import { test as base, type APIRequestContext } from '@playwright/test';
import { createApiRequestContext } from '../api/apiBase';
import { getApiConfigFromEnv } from '../support/api-config';
import { registerAuthentication } from '../support/authenticationInterceptor';
import { getServiceConfigFromEnv } from '../support/test-config';
import type { ApiConfig, Terminology } from '../support/types';
import { DashboardPage } from '../pages/DashboardPage';
import { ContributorsPage } from '../pages/ContributorsPage';
import { ContributorsInvitePage } from '../pages/ContributorsInvitePage';

interface Fixtures {
  terminology: Terminology;
  dashboardPage: DashboardPage;
  contributorsPage: ContributorsPage;
  contributorsInvitePage: ContributorsInvitePage;
}

interface WorkerFixtures {
  apiConfig: ApiConfig;
  apiClient: APIRequestContext;
}

export const test = base.extend<Fixtures, WorkerFixtures>({
  apiConfig: [
    async ({}, use) => {
      await use(getApiConfigFromEnv());
    },
    { scope: 'worker' },
  ],
  apiClient: [
    async ({ playwright, apiConfig }, use) => {
      const context = await createApiRequestContext(playwright, apiConfig);
      await use(context);
      await context.dispose();
    },
    { scope: 'worker' },
  ],
  page: async ({ page }, use) => {
    // Skip SignalR when running under Cypress to avoid WebSocket proxy issues
    await page.addInitScript('window.Cypress = true');
    await use(page);
  },
  context: async ({ context }, use) => {
    const serviceConfig = getServiceConfigFromEnv();
    await registerAuthentication(context, serviceConfig);
    await use(context);
  },
  terminology: async ({}, use) => {
    await use(getServiceConfigFromEnv().terminology);
  },
  dashboardPage: async ({ page, terminology }, use) => {
    await use(new DashboardPage(page, terminology));
  },
  contributorsPage: async ({ page, terminology }, use) => {
    await use(new ContributorsPage(page, terminology));
  },
  contributorsInvitePage: async ({ page, terminology }, use) => {
    await use(new ContributorsInvitePage(page, terminology));
  },
});

export { expect } from '@playwright/test';
