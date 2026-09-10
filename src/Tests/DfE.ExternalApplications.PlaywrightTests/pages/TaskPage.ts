import { expect, Locator } from '@playwright/test';
import { FormPage } from './FormPage';

export abstract class TaskPage extends FormPage {
  protected abstract readonly taskItem: string;

  async open(): Promise<void> {
    await this.taskLink().click();
  }

  async expectCompleted(): Promise<void> {
    await expect(this.taskStatus()).toContainText('Completed');
  }

  private taskItemLocator(): Locator {
    return this.byId(this.taskItem);
  }

  private taskLink(): Locator {
    return this.taskItemLocator().getByRole('link').first();
  }

  private taskStatus(): Locator {
    return this.taskItemLocator().locator('.govuk-task-list__status');
  }
}
