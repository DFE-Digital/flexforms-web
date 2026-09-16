import type { Locator } from '@playwright/test';
import { BasePage } from './BasePage';

export class ContributorsInvitePage extends BasePage {
  async fillInvite(name: string, email: string): Promise<void> {
    await this.nameInput().fill(name);
    await this.emailAddressInput().fill(email);
  }

  async sendInvite(): Promise<void> {
    await this.sendInviteButton().click();
  }

  private nameInput(): Locator {
    return this.page.getByLabel('Full name');
  }

  private emailAddressInput(): Locator {
    return this.page.getByLabel('Email address');
  }

  private sendInviteButton(): Locator {
    return this.page.getByRole('button', { name: 'Send email invite' });
  }
}
