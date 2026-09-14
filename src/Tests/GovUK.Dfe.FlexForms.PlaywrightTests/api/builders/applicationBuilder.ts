import { CreateApplicationRequest } from '../types';

export class ApplicationBuilder {
  public static createApplicationRequest(templateId: string, initialResponseBody = '{}'): CreateApplicationRequest {
    return {
      templateId,
      initialResponseBody,
    };
  }
}
