import { TaskPage } from '../TaskPage';

export class ConversationDetailsTask extends TaskPage {
  protected readonly taskItem = 'group-engagement-details-task-conversation-details';

  async complete(): Promise<void> {
    await this.byId('field-visit-purpose-field-change-link').click();
    await this.byIdData_('visit-purpose-field').fill('Test purpose of the visit');
    await this.saveAndContinue();

    await this.byId('field-visit-notes-field-change-link').click();
    await this.byIdData_('visit-notes-field').fill('Test notes of the visit');
    await this.saveAndContinue();

    await this.markCompleteAndSave();
  }
}
