import { expect, Locator } from '@playwright/test';
import { FormPage } from './FormPage';

export abstract class TaskPage extends FormPage {
  protected abstract readonly taskItem: string;

  async open(): Promise<void> {
    await this.taskLink().click();
  }

  async unableToOpen(): Promise<void> {
    await expect(this.taskItemLocator()).toBeVisible();
    await expect(this.taskName()).toBeVisible();
    await expect(this.taskLinks()).toHaveCount(0);
  }

  async expectCompleted(): Promise<void> {
    await expect(this.taskStatus()).toContainText('Completed');
  }

  private taskItemLocator(): Locator {
    return this.byId(this.taskItem);
  }

  private taskLinks(): Locator {
    return this.taskItemLocator().getByRole('link');
  }

  private taskLink(): Locator {
    return this.taskLinks().first();
  }

  private taskName(): Locator {
    return this.taskItemLocator().locator('.govuk-task-list__link');
  }

  private taskStatus(): Locator {
    return this.taskItemLocator().locator('.govuk-task-list__status');
  }
}
