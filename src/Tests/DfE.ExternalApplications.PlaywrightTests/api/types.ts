export type applicationStatus = 'Created' | 'InProgress' | 'Submitted' | 'Deleted';

export interface CreateApplicationRequest {
  templateId: string;
  initialResponseBody: string | null;
}

export interface CreateApplicationResponse {
  applicationId: string;
  applicationReference: string;
  status?: string;
}

export interface ExchangeTokenRequest {
  accessToken: string;
}

export interface ExchangeTokenResponse {
  access_token?: string;
  token_type?: string;
  expires_in?: number;
}

export interface CustomApplicationStatus {
  customApplicationStatusId: string;
  templateId: string;
  applicationStatus: applicationStatus;
  label: string;
  createdOn: string;
  createdBy: string;
}

export type CustomApplicationStatusResponse = CustomApplicationStatus[];
