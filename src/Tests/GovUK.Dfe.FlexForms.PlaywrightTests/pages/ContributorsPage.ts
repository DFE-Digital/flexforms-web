import { expect, type Locator } from '@playwright/test';
import { BasePage } from './BasePage';

export class ContributorsPage extends BasePage {
  async addContributor(): Promise<void> {
    await this.addContributorButton().click();
  }

  async proceedToForm(): Promise<void> {
    await this.proceedToFormButton().click();
  }

  async expectContributor(index: number, name: string, email: string): Promise<void> {
    const row = this.contributorRow(index);
    await expect(row).toContainText(name);
    await expect(row).toContainText(email);
  }

  private addContributorButton(): Locator {
    return this.page.getByRole('button', { name: 'Add a contributor' });
  }

  private proceedToFormButton(): Locator {
    return this.byId('proceed-to-application-form');
  }

  private contributorRow(index: number): Locator {
    return this.byId(`contributor-${index}`);
  }
}
