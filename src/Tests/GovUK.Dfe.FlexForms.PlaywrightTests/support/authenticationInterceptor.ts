import type { BrowserContext } from '@playwright/test';
import { getDefaultAuthUser } from './auth-users';
import type { AuthUser, ServiceConfig } from './types';

const identityByContext = new WeakMap<BrowserContext, AuthUser>();

export function setContextAuthUser(context: BrowserContext, user: AuthUser): void {
  identityByContext.set(context, user);
}

export async function registerAuthentication(context: BrowserContext, config: ServiceConfig): Promise<void> {
  setContextAuthUser(context, getDefaultAuthUser());

  await context.route(`${config.url}/**`, async (route) => {
    const user = identityByContext.get(context) ?? getDefaultAuthUser();
    const headers = {
      ...route.request().headers(),
      'x-service-email': user.email,
      'x-service-api-key': user.apiKey,
      'X-Tenant-ID': config.tenantId,
    };

    await route.continue({ headers });
  });
}
