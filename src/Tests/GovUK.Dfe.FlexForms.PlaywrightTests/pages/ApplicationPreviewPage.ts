import { expect, type Locator } from '@playwright/test';
import { FormPage } from './FormPage';

export class ApplicationPreviewPage extends FormPage {
  async submit(): Promise<void> {
    await this.submitApplicationButton().click();
  }

  async submitButtonNotVisible(): Promise<void> {
    await expect(this.preview()).toBeVisible();
    await expect(this.submitApplicationButton()).toHaveCount(0);
  }

  async expectNonLeadApplicantCannotSubmit(singular: string): Promise<void> {
    await this.submitButtonNotVisible();
    await expect(this.leadApplicantSubmitMessage(singular)).toBeVisible();
  }

  async expectSubmitted(): Promise<void> {
    await expect(this.page).toHaveURL(/\/application-submitted\//);
    await expect(this.submittedHeading()).toContainText('submitted');
  }

  private preview(): Locator {
    return this.page.locator('[data-application-preview="true"]');
  }

  private submitApplicationButton(): Locator {
    return this.byId('submit-application-button');
  }

  private leadApplicantSubmitMessage(singular: string): Locator {
    return this.page.getByText(`Only the lead applicant can submit this ${singular}.`, { exact: true });
  }

  private submittedHeading(): Locator {
    return this.page.getByRole('heading', { name: /submitted/i });
  }
}
