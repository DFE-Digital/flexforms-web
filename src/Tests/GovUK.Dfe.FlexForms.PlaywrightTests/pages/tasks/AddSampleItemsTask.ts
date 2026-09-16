import { TaskPage } from '../TaskPage';

export class AddSampleItemsTask extends TaskPage {
  protected readonly taskItem = 'group-collection-flows-task-add-sample-items';

  async complete(establishment: string): Promise<void> {
    await this.byId('sample-items-flow-add-item').click();

    await this.byIdData_('itemName').fill('Sample Item 1');

    await this.byIdData_('itemCategory').selectOption('Category one');
    await this.searchAutocomplete('Data_itemEstablishmentSearch-complex-field', establishment);
    await this.confirmYesAndContinue();

    await this.markCompleteAndSave();
  }
}
