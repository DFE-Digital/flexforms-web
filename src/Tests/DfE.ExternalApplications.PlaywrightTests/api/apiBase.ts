import type { APIRequestContext, PlaywrightWorkerArgs } from '@playwright/test';
import { getInternalUserToken } from './auth/internalUserToken';
import type { ApiConfig } from '../support/types';

export async function createApiRequestContext(
  playwright: PlaywrightWorkerArgs['playwright'],
  config: ApiConfig,
): Promise<APIRequestContext> {
  const token = await getInternalUserToken(config);

  return playwright.request.newContext({
    baseURL: config.baseUrl,
    extraHTTPHeaders: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      'X-Tenant-ID': config.tenantId,
    },
    ignoreHTTPSErrors: true,
  });
}

export async function apiRequest<TResponse>(
  request: APIRequestContext,
  path: string,
  options: {
    method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
    data?: unknown;
  } = {},
): Promise<TResponse> {
  const response = await request.fetch(path, {
    method: options.method ?? 'GET',
    data: options.data,
  });

  if (!response.ok()) {
    throw new Error(`API request failed (${response.status()}): ${await response.text()}`);
  }

  return (await response.json()) as Promise<TResponse>;
}
