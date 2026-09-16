import './load-env';
import { getDefaultAuthUser } from './auth-users';
import { requireEnvironmentVariable, requireService } from './environment';
import type { ServiceConfig, ServiceName } from './types';

function normalizeUrl(url: string): string {
  return url.replace(/\/$/, '');
}

export function createServiceConfig(serviceName: ServiceName): ServiceConfig {
  const defaultUser = getDefaultAuthUser();

  return {
    name: serviceName,
    url: normalizeUrl(requireEnvironmentVariable('BASE_URL')),
    username: defaultUser.email,
    apiKey: defaultUser.apiKey,
    tenantId: requireEnvironmentVariable('TENANT_ID'),
    terminology: {
      singular: requireEnvironmentVariable('TERMINOLOGY_SINGULAR'),
      plural: requireEnvironmentVariable('TERMINOLOGY_PLURAL'),
    },
  };
}

export function getServiceConfigFromEnv(): ServiceConfig {
  return createServiceConfig(requireService());
}
