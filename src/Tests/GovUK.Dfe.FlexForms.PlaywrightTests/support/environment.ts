import type { ServiceName } from './types';

export const applications: readonly ServiceName[] = ['Transfers', 'Lsrp', 'Visits', 'TestService'];

export function requireEnvironmentVariable(name: string): string {
  const value = optionalEnvironmentVariable(name);

  if (!value) {
    throw new Error(`${name} is required. Set it in .env locally or as a GitHub environment variable in CI.`);
  }

  return value;
}

export function optionalEnvironmentVariable(name: string): string | undefined {
  const value = process.env[name]?.trim();
  return value || undefined;
}

export function requireService(): ServiceName {
  const service = requireEnvironmentVariable('SERVICE');

  if (!applications.includes(service as ServiceName)) {
    throw new Error(`SERVICE must be one of: ${applications.join(', ')}`);
  }

  return service as ServiceName;
}
