import type { ServiceName } from './types';

export const applications: readonly ServiceName[] = ['Transfers', 'Lsrp', 'RGVisits', 'TestService'];

export function requireEnvironmentVariable(name: string): string {
  const value = process.env[name]?.trim();

  if (!value) {
    throw new Error(`${name} is required. Set it in .env locally or as a GitHub environment variable in CI.`);
  }

  return value;
}

export function requireService(): ServiceName {
  const service = requireEnvironmentVariable('SERVICE');

  if (!applications.includes(service as ServiceName)) {
    throw new Error(`SERVICE must be one of: ${applications.join(', ')}`);
  }

  return service as ServiceName;
}
