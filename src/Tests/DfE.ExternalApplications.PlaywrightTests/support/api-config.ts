import './load-env';
import { requireEnvironmentVariable } from './environment';
import { requireApiKey } from './test-config';
import type { ApiConfig } from './types';

function resolveTokenLifetimeMinutes(): number {
  const value = process.env.INTERNAL_AUTH_TOKEN_LIFETIME_MINUTES?.trim();
  if (!value) {
    return 10;
  }

  const tokenLifetimeMinutes = Number(value);
  if (!Number.isInteger(tokenLifetimeMinutes) || tokenLifetimeMinutes <= 0) {
    throw new Error('INTERNAL_AUTH_TOKEN_LIFETIME_MINUTES must be a positive integer.');
  }

  return tokenLifetimeMinutes;
}

export function createApiConfig(): ApiConfig {
  return {
    baseUrl: requireEnvironmentVariable('API_BASE_URL').replace(/\/$/, ''),
    tenantId: requireEnvironmentVariable('TENANT_ID'),
    serviceEmail: requireEnvironmentVariable('SERVICE_EMAIL'),
    serviceApiKey: requireApiKey(),
    templateId: requireEnvironmentVariable('TEMPLATE_ID'),
    internalServiceAuth: {
      secretKey: requireEnvironmentVariable('JWT_SIGNING_KEY'),
      issuer: requireEnvironmentVariable('INTERNAL_AUTH_ISSUER'),
      audience: requireEnvironmentVariable('INTERNAL_AUTH_AUDIENCE'),
      tokenLifetimeMinutes: resolveTokenLifetimeMinutes(),
    },
    authProviderApiKey: requireEnvironmentVariable('AUTH_PROVIDER_API_KEY'),
  };
}

export function getApiConfigFromEnv(): ApiConfig {
  return createApiConfig();
}
