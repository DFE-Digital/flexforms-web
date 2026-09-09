import { expect } from '@playwright/test';
import { FormPage } from './FormPage';

export class ApplicationPreviewPage extends FormPage {
  async submit(): Promise<void> {
    await this.byId('submit-application-button').click();
  }

  async expectSubmitted(): Promise<void> {
    await expect(this.page).toHaveURL(/\/application-submitted\//);
    await expect(this.page.locator('.govuk-panel__title')).toContainText('submitted');
  }
}
