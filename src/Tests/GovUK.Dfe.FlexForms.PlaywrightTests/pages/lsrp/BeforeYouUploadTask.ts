import { TaskPage } from '../TaskPage';

export class BeforeYouUploadTask extends TaskPage {
  protected readonly taskItem = 'group-financial-summary-task-before-you-upload';

  async complete(localAuthority: string): Promise<void> {
    await this.searchAutocomplete('Data_localAuthoritySearch-field-flow-complex-field', localAuthority);
    await this.confirmYesAndContinue();

    await this.markCompleteAndSave();
  }
}