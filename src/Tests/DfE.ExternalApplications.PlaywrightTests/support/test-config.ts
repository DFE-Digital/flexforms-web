import './load-env';
import { requireEnvironmentVariable, requireService } from './environment';
import type { ServiceConfig, ServiceName } from './types';

function normalizeUrl(url: string): string {
  return url.replace(/\/$/, '');
}

export function requireApiKey(): string {
  return requireEnvironmentVariable('SERVICE_API_KEY');
}

export function createServiceConfig(serviceName: ServiceName): ServiceConfig {
  return {
    name: serviceName,
    url: normalizeUrl(requireEnvironmentVariable('BASE_URL')),
    username: requireEnvironmentVariable('SERVICE_EMAIL'),
    apiKey: requireApiKey(),
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
