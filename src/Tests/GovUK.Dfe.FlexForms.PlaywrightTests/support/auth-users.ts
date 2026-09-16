import './load-env';
import { requireEnvironmentVariable } from './environment';
import type { AuthUser } from './types';

export const authUserNames = ['default', 'admin', 'caseworker'] as const;
export type AuthUserName = (typeof authUserNames)[number];

const authUserEnvironmentVariables: Record<AuthUserName, { email: string; apiKey: string }> = {
  default: { email: 'DEFAULT_USER_EMAIL', apiKey: 'DEFAULT_USER_API_KEY' },
  admin: { email: 'ADMIN_EMAIL', apiKey: 'ADMIN_API_KEY' },
  caseworker: { email: 'CASEWORKER_EMAIL', apiKey: 'CASEWORKER_API_KEY' },
};

function loadAuthUser(name: AuthUserName): AuthUser {
  const variables = authUserEnvironmentVariables[name];

  return {
    name,
    email: requireEnvironmentVariable(variables.email),
    apiKey: requireEnvironmentVariable(variables.apiKey),
  };
}

export function getDefaultAuthUser(): AuthUser {
  return loadAuthUser('default');
}

export function resolveAuthUser(userName?: string): AuthUser {
  const name = (userName?.trim().toLowerCase() || 'default') as AuthUserName;

  if (!authUserNames.includes(name)) {
    throw new Error(`Unknown auth user '${userName}'. Expected one of: ${authUserNames.join(', ')}`);
  }

  return loadAuthUser(name);
}
