import { expect } from '@playwright/test';
import { BasePage } from './BasePage';

export class ContributorsPage extends BasePage {
  private static readonly selectors = {
    addContributorButton: '#add-a-contributor',
    proceedToForm: '#proceed-to-application-form',
    contributorRow: (index: number) => `#contributor-${index}`,
  } as const;

  async addContributor(): Promise<void> {
    await this.page.locator(ContributorsPage.selectors.addContributorButton).click();
  }

  async proceedToForm(): Promise<void> {
    await this.page.locator(ContributorsPage.selectors.proceedToForm).click();
  }

  async expectContributor(index: number, name: string, email: string): Promise<void> {
    const row = this.page.locator(ContributorsPage.selectors.contributorRow(index));
    await expect(row).toContainText(name);
    await expect(row).toContainText(email);
  }
}
