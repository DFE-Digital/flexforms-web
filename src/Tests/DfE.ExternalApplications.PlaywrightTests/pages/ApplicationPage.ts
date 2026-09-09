import { BasePage } from './BasePage';

export class ApplicationPage extends BasePage {
  async inviteContributors(): Promise<void> {
    await this.page.locator('#invite-contributors').click();
  }
}
