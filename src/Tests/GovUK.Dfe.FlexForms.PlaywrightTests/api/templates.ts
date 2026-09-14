import { CustomApplicationStatusResponse } from './types';
import { apiRequest } from './apiBase';
import { APIRequestContext } from '@playwright/test';

export async function getTemplateCustomStatuses(
  request: APIRequestContext,
  templateId: string,
): Promise<CustomApplicationStatusResponse> {
  return apiRequest<CustomApplicationStatusResponse>(request, `/v1/Templates/${templateId}/custom-statuses`);
}
