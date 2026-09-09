import type { APIRequestContext } from '@playwright/test';
import { apiRequest } from './apiBase';
import type { CreateApplicationRequest, CreateApplicationResponse } from './types';

export async function createApplication(
  request: APIRequestContext,
  body: CreateApplicationRequest,
): Promise<CreateApplicationResponse> {
  return apiRequest<CreateApplicationResponse>(request, '/v1/applications', {
    method: 'POST',
    data: body,
  });
}
