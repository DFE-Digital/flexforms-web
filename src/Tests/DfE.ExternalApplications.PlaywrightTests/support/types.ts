export type ServiceName = 'Transfers' | 'Lsrp' | 'RGVisits' | 'TestService';

export interface Terminology {
  singular: string;
  plural: string;
}

export interface AuthUser {
  name: string;
  email: string;
  apiKey: string;
}

export interface ServiceConfig {
  name: ServiceName;
  url: string;
  username: string;
  apiKey: string;
  tenantId: string;
  terminology: Terminology;
}

export interface InternalServiceAuthSettings {
  secretKey: string;
  issuer: string;
  audience: string;
  tokenLifetimeMinutes: number;
}

export interface ApiConfig {
  baseUrl: string;
  tenantId: string;
  serviceEmail: string;
  serviceApiKey: string;
  templateId: string;
  internalServiceAuth: InternalServiceAuthSettings;
  authProviderApiKey: string;
}
