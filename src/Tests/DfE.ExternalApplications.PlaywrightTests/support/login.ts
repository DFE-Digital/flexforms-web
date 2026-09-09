import type { Page } from '@playwright/test';
import { getServiceConfigFromEnv } from './test-config';

export async function login(page: Page): Promise<void> {
  await page.context().clearCookies();
  await page.context().clearPermissions();
  await page.context().addCookies([
    {
      name: '.AspNet.Consent',
      value: 'yes',
      url: getServiceConfigFromEnv().url,
    },
  ]);
  await page.goto('/');
  await page.waitForURL(/\/applications\/dashboard/);
}
