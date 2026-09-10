import { TaskPage } from '../TaskPage';

export class ReasonsAndBenefitsOutgoingPage extends TaskPage {
  protected readonly taskItem = 'group-about-transferring-academies-task-reason-and-benefits';

  async complete(): Promise<void> {
    await this.byId('field-reasonandbenefitsacademiesstrategicneeds-change-link').click();
    await this.byId('Data_reasonAndBenefitsAcademiesStrategicNeeds').fill('Strategic needs testing text');
    await this.saveAndContinue();

    await this.byId('field-reasonandbenefitsacademiesmaintainimprove-change-link').click();
    await this.byId('Data_reasonAndBenefitsAcademiesMaintainImprove').fill('Benefits testing text');
    await this.saveAndContinue();

    await this.markCompleteAndSave();
  }
}
