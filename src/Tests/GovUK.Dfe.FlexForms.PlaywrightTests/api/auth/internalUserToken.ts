import jwt from 'jsonwebtoken';
import type { ApiConfig } from '../../support/types';
import { exchangeForInternalUserToken } from './exchange';
import { generateInternalServiceToken } from './token';

interface CachedToken {
  value: string;
  expiresAtMs: number;
}

const EXPIRY_BUFFER_MS = 2 * 60 * 1000;

const cachedInternalUserTokens = new Map<string, CachedToken>();

function cacheKey(config: ApiConfig): string {
  return `${config.tenantId}:${config.serviceEmail.toLowerCase()}`;
}

function isCacheValid(cache: CachedToken | null): cache is CachedToken {
  return cache !== null && Date.now() + EXPIRY_BUFFER_MS < cache.expiresAtMs;
}

function resolveTokenExpiryMs(token: string): number {
  const decoded = jwt.decode(token);

  if (!decoded || typeof decoded === 'string' || typeof decoded.exp !== 'number') {
    throw new Error('Unable to decode internal user token expiry');
  }

  return decoded.exp * 1000;
}

export async function getInternalUserToken(config: ApiConfig): Promise<string> {
  const key = cacheKey(config);
  const cachedInternalUserToken = cachedInternalUserTokens.get(key) ?? null;

  if (isCacheValid(cachedInternalUserToken)) {
    return cachedInternalUserToken.value;
  }

  const signInToken = generateInternalServiceToken(config);
  const internalUserToken = await exchangeForInternalUserToken(config, signInToken);

  cachedInternalUserTokens.set(key, {
    value: internalUserToken,
    expiresAtMs: resolveTokenExpiryMs(internalUserToken),
  });

  return internalUserToken;
}
