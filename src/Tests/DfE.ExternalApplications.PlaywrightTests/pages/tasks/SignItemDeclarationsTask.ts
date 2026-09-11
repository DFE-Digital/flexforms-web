import { TaskPage } from '../TaskPage';

export class SignItemDeclarationsTask extends TaskPage {
  protected readonly taskItem = 'group-collection-flows-task-sign-item-declarations';

  async complete(establishment: string): Promise<void> {
    await this.byId(`view-${establishment}`).click();

    await this.byIdData_('itemDeclaration').click();

    await this.byIdData_('declarationName').fill('Test User');
    await this.byIdData_('declarationAgreed_').check();
    await this.enterDate('Data_declarationSignedDate', '1', '1', '2027');
    await this.saveAndContinue();

    await this.markCompleteAndSave();
  }
}
