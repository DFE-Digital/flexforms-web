import type { Page } from '@playwright/test';
import { resolveAuthUser, type AuthUserName } from './auth-users';
import { setContextAuthUser } from './authenticationInterceptor';
import { getServiceConfigFromEnv } from './test-config';

export async function login(page: Page, userName?: AuthUserName): Promise<void> {
  const user = resolveAuthUser(userName);
  setContextAuthUser(page.context(), user);

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
