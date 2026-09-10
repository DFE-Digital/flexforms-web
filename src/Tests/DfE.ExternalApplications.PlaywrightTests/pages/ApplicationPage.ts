import type { Locator } from '@playwright/test';
import { BasePage } from './BasePage';

export class ApplicationPage extends BasePage {
  async inviteContributors(): Promise<void> {
    await this.inviteContributorsButton().click();
  }

  async goToSection(name: string): Promise<void> {
    await this.sectionLink(name).click();
  }

  private inviteContributorsButton(): Locator {
    return this.page.getByRole('button', { name: 'Invite contributors' });
  }

  private sectionLink(name: string): Locator {
    return this.page.getByRole('link', { name });
  }
}
