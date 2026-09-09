import { BasePage } from './BasePage';

export class ContributorsInvitePage extends BasePage {
  private static readonly selectors = {
    name: '#Name',
    emailAddress: '#EmailAddress',
    sendInviteButton: '#send-email-invite',
  } as const;

  async fillInvite(name: string, email: string): Promise<void> {
    await this.page.locator(ContributorsInvitePage.selectors.name).fill(name);
    await this.page.locator(ContributorsInvitePage.selectors.emailAddress).fill(email);
  }

  async sendInvite(): Promise<void> {
    await this.page.locator(ContributorsInvitePage.selectors.sendInviteButton).click();
  }
}
