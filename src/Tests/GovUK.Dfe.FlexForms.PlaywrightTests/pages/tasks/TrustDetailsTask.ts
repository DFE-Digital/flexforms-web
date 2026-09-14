import { TaskPage } from '../TaskPage';

export class TrustDetailsTask extends TaskPage {
  protected readonly taskItem = 'group-collection-flows-task-trust-details';

  async complete(trust: string): Promise<void> {
    await this.byId('details-of-trust-flow-add-item').click();

    await this.searchAutocomplete('Data_trustSearch-complex-field', trust);
    await this.confirmYesAndContinue();
    await this.markCompleteAndSave();
  }
}
