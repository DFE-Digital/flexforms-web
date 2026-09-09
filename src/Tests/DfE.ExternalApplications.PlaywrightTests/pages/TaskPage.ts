import { expect, Locator } from '@playwright/test';
import { FormPage } from './FormPage';

export abstract class TaskPage extends FormPage {
  protected abstract readonly taskItem: string;

  private taskItemLocator(): Locator {
    return this.byId(this.taskItem);
  }

  async open(): Promise<void> {
    await this.taskItemLocator().getByRole('link').first().click();
  }

  async expectCompleted(): Promise<void> {
    const status = this.taskItemLocator().locator('.govuk-task-list__status');
    await expect(status).toContainText('Completed');
  }
}
