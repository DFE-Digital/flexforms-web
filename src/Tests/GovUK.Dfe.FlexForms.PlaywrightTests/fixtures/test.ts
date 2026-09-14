import { test as base, type APIRequestContext } from '@playwright/test';
import { createApiRequestContext } from '../api/apiBase';
import { apiConfigForUser, getApiConfigFromEnv } from '../support/api-config';
import { registerAuthentication } from '../support/authenticationInterceptor';
import { getServiceConfigFromEnv } from '../support/test-config';
import type { ApiConfig, Terminology } from '../support/types';

interface Fixtures {
  terminology: Terminology;
}

interface WorkerFixtures {
  apiConfig: ApiConfig;
  apiClient: APIRequestContext;
  adminApiClient: APIRequestContext;
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
  adminApiClient: [
    async ({ playwright, apiConfig }, use) => {
      const context = await createApiRequestContext(playwright, apiConfigForUser('admin', apiConfig));
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
});

export { expect } from '@playwright/test';
