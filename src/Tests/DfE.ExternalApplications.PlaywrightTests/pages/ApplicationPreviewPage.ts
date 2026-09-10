import { expect, type Locator } from '@playwright/test';
import { FormPage } from './FormPage';

export class ApplicationPreviewPage extends FormPage {
  async submit(): Promise<void> {
    await this.submitApplicationButton().click();
  }

  async expectSubmitted(): Promise<void> {
    await expect(this.page).toHaveURL(/\/application-submitted\//);
    await expect(this.submittedHeading()).toContainText('submitted');
  }

  private submitApplicationButton(): Locator {
    return this.byId('submit-application-button');
  }

  private submittedHeading(): Locator {
    return this.page.getByRole('heading', { name: /submitted/i });
  }
}
