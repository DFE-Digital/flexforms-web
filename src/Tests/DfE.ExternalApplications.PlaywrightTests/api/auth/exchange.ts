import type { ApiConfig } from '../../support/types';
import type { ExchangeTokenRequest, ExchangeTokenResponse } from '../types';

export function resolveAccessToken(response: ExchangeTokenResponse): string {
  const accessToken = response.access_token;

  if (!accessToken) {
    throw new Error('Token exchange response did not include an access token');
  }

  return accessToken;
}

export async function exchangeForInternalUserToken(config: ApiConfig, signInToken: string): Promise<string> {
  const body: ExchangeTokenRequest = { accessToken: signInToken };

  const response = await fetch(`${config.baseUrl}/v1/Tokens/exchange`, {
    method: 'POST',
    headers: {
      'X-Api-Key': config.authProviderApiKey,
      'Content-Type': 'application/json',
      'X-Tenant-ID': config.tenantId,
      'x-service-email': config.serviceEmail,
      'x-service-api-key': config.serviceApiKey,
    },
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    throw new Error(`Token exchange failed (${response.status}): ${await response.text()}`);
  }

  const exchangeResponse = (await response.json()) as ExchangeTokenResponse;
  return resolveAccessToken(exchangeResponse);
}
