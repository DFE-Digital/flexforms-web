import type { APIRequestContext } from '@playwright/test';
import { apiRequest } from './apiBase';
import { ApplicationFileResponse, CreateApplicationRequest, CreateApplicationResponse } from './types';

export async function createApplication(
  request: APIRequestContext,
  body: CreateApplicationRequest,
): Promise<CreateApplicationResponse> {
  return apiRequest<CreateApplicationResponse>(request, '/v1/applications', {
    method: 'POST',
    data: body,
  });
}

export async function getApplicationByRef(
  request: APIRequestContext,
  applicationReference: string,
): Promise<CreateApplicationResponse> {
  return apiRequest<CreateApplicationResponse>(request, `/v1/applications/reference/${applicationReference}`);
}

export async function getFiles(request: APIRequestContext, applicationId: string): Promise<ApplicationFileResponse> {
  return apiRequest<ApplicationFileResponse>(request, `/v1/applications/${applicationId}/files`);
}
