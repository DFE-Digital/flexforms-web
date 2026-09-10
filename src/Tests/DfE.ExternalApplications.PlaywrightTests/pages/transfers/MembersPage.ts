import { expect } from '@playwright/test';
import { TaskPage } from '../TaskPage';

export class MembersPage extends TaskPage {
  protected readonly taskItem = 'group-about-the-trust-that-academies-are-joining-task-members';

  private async expectSummary(): Promise<void> {
    await expect(this.page).toHaveURL(/\/members$/);
  }

  async addExistingMember(name: string): Promise<void> {
    await this.expectSummary();
    await this.byId('membersAfterTransfer-add-item').click();
    await this.byId('Data_memberName').first().fill(name);
    await this.saveAndContinue();
    await this.byId('Data_existingMember_').first().check();
    await this.saveAndContinue();
    await this.byId('Data_additionalRoles_').check();
    await this.saveAndContinue();
    await this.expectSummary();
  }

  async addNewMember(name: string, pastRoles: string): Promise<void> {
    await this.expectSummary();
    await this.byId('membersAfterTransfer-add-item').click();
    await this.byId('Data_memberName').first().fill(name);
    await this.saveAndContinue();
    await this.byId('Data_existingMember_-2').first().check();
    await this.saveAndContinue();
    await this.byId('Data_pastRoles').first().fill(pastRoles);
    await this.saveAndContinue();
    await this.byId('Data_additionalRoles_-2').check();
    await this.saveAndContinue();
    await this.expectSummary();
  }

  async addLeavingMember(name: string): Promise<void> {
    await this.expectSummary();
    await this.byId('membersLeaving-add-item').click();
    await this.byId('Data_memberLeavingName').first().fill(name);
    await this.saveAndContinue();
    await this.expectSummary();
  }

  async complete(): Promise<void> {
    await this.markCompleteAndSave();
  }
}
