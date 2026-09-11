import { TaskPage } from '../TaskPage';

interface ConfiguredFieldsData {
  academy: string;
  localAuthority: string;
  diocese: string;
  mp: string;
}

export class ConfiguredFieldsTask extends TaskPage {
  protected readonly taskItem = 'group-configured-fields-task-complete-configured-fields';

  async complete(data: ConfiguredFieldsData): Promise<void> {
    await this.searchAutocomplete('Data_establishmentSearch-complex-field', data.academy);
    await this.confirmYesAndContinue();

    await this.searchAutocomplete('Data_localAuthoritySearch-complex-field', data.localAuthority);
    await this.confirmYesAndContinue();

    await this.searchAutocomplete('Data_dioceseSearch-complex-field', data.diocese);
    await this.confirmYesAndContinue();

    await this.searchAutocomplete('Data_mpSearch-complex-field', data.mp);
    await this.confirmYesAndContinue();

    await this.uploadFile('supportingDocuments');
    await this.page.pause();
    await this.markCompleteAndSave();
  }
}
