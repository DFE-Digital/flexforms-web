import { expect } from '@playwright/test';
import { FormPage } from './FormPage';

export class TaskListPage extends FormPage {
  async expectLoaded(): Promise<void> {
    await expect(this.page).toHaveURL(/\/applications\/[^/]+$/);
  }

  async reviewApplication(): Promise<void> {
    await this.byId('review-application-button').click();
  }
}
