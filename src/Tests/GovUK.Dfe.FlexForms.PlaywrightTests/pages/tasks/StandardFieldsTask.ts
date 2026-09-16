import { TaskPage } from '../TaskPage';

export class StandardFieldsTask extends TaskPage {
  protected readonly taskItem = 'group-standard-fields-task-complete-standard-fields';

  async complete(): Promise<void> {
    await this.byIdData_('fullName').fill('Test User');
    await this.saveAndContinue();
    await this.byIdData_('phoneNumber').fill('07700 900123');
    await this.byIdData_('emailAddress').fill('test@test.com');
    await this.saveAndContinue();
    await this.byIdData_('optionalReference').fill('Optional Reference');
    await this.saveAndContinue();
    await this.byIdData_('additionalComments').fill('Additional Comments \n with a new line');
    await this.saveAndContinue();
    await this.byIdData_('applicationReason').fill('Application Reason');
    await this.saveAndContinue();
    await this.confirmYesAndContinue();
    await this.byIdData_('followUpDetails').fill('Follow Up Details');
    await this.saveAndContinue();
    await this.byIdData_('topics_-2').check();
    await this.byIdData_('topics_-3').check();
    await this.saveAndContinue();
    await this.byIdData_('region').selectOption('South West');
    await this.saveAndContinue();
    await this.enterDate('Data_proposedDate', '11', '11', '2025');
    await this.saveAndContinue();

    await this.markCompleteAndSave();
  }
}
