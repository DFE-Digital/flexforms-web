import { expect, type Locator } from '@playwright/test';
import { FormPage } from './FormPage';

export class TaskListPage extends FormPage {
  async expectLoaded(): Promise<void> {
    await expect(this.page).toHaveURL(/\/applications\/[^/]+$/);
  }

  async reviewApplication(): Promise<void> {
    await this.reviewApplicationButton().click();
  }

  private reviewApplicationButton(): Locator {
    return this.byId('review-application-button');
  }
}
