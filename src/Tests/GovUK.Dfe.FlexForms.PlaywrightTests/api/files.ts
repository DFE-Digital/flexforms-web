import { APIRequestContext, type Page } from '@playwright/test';
import '../support/load-env';
import { optionalEnvironmentVariable } from '../support/environment';
import { FileValidationRequest, FileValidationResult } from './types';
import { apiRequest } from './apiBase';
import { getApplicationByRef, getFiles } from './application';

const reservedApplicationPathSegments = new Set(['dashboard']);

export function applicationReferenceFromUrl(url: string): string {
  const pathname = new URL(url).pathname;
  const match = /^\/applications\/([^/]+)/.exec(pathname);
  const applicationRef = match?.[1];

  if (!applicationRef || reservedApplicationPathSegments.has(applicationRef.toLowerCase())) {
    throw new Error(`Could not read application reference from URL: ${url}`);
  }

  return applicationRef;
}

export async function validationResult(
  request: APIRequestContext,
  body: FileValidationRequest,
  fileId: string,
): Promise<FileValidationResult> {
  const fileValidationApiKey = optionalEnvironmentVariable('FILE_VALIDATION_API_KEY');

  return apiRequest<FileValidationResult>(request, `/v1/integrations/files/${fileId}/validation-result`, {
    method: 'POST',
    data: body,
    headers: fileValidationApiKey ? { 'X-Api-Key': fileValidationApiKey } : undefined,
  });
}

export async function validateValidFileForApplication(request: APIRequestContext, page: Page): Promise<void> {
  const applicationRef = applicationReferenceFromUrl(page.url());
  const applicationId = await getApplicationByRef(request, applicationRef).then((app) => app.applicationId);
  const files = await getFiles(request, applicationId);
  const fileId = files[0]?.id;

  if (!fileId) {
    throw new Error(`No files found for application ${applicationRef}`);
  }

  await validationResult(request, { isValid: true, message: 'File is valid', source: 'test-source' }, fileId);
}
