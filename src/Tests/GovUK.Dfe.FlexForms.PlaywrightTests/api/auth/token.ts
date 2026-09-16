import jwt from 'jsonwebtoken';
import type { ApiConfig } from '../../support/types';

const claimTypes = {
  email: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress',
  nameIdentifier: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier',
  name: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name',
} as const;

/**
 * Signs an internal service JWT matching InternalServiceAuthenticationService.GenerateServiceTokenAsync.
 */
export function generateInternalServiceToken(config: ApiConfig): string {
  const { serviceEmail, internalServiceAuth } = config;
  const { secretKey, issuer, audience, tokenLifetimeMinutes } = internalServiceAuth;

  return jwt.sign(
    {
      sub: serviceEmail,
      email: serviceEmail,
      name: serviceEmail,
      service_type: 'internal',
      [claimTypes.email]: serviceEmail,
      [claimTypes.nameIdentifier]: serviceEmail,
      [claimTypes.name]: serviceEmail,
    },
    secretKey,
    {
      algorithm: 'HS256',
      issuer,
      audience,
      expiresIn: tokenLifetimeMinutes * 60,
    },
  );
}
