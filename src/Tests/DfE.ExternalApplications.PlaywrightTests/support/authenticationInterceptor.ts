import type { BrowserContext } from '@playwright/test';
import type { ServiceConfig } from './types';

export async function registerAuthentication(context: BrowserContext, config: ServiceConfig): Promise<void> {
  await context.route(`${config.url}/**`, async (route) => {
    const headers = {
      ...route.request().headers(),
      'x-service-email': config.username,
      'x-service-api-key': config.apiKey,
      'X-Tenant-ID': config.tenantId,
    };

    await route.continue({ headers });
  });
}
