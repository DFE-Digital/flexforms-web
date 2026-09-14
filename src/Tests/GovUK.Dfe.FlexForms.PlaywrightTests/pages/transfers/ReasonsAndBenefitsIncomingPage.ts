import { TaskPage } from '../TaskPage';

export class ReasonsAndBenefitsIncomingPage extends TaskPage {
  protected readonly taskItem = 'group-about-the-trust-that-academies-are-joining-task-reason-and-benefits';

  async complete(): Promise<void> {
    await this.byId('field-reasonandbenefitstruststrategicneeds-change-link').click();
    await this.byId('Data_reasonAndBenefitsTrustStrategicNeeds').fill('Strategic needs testing text');
    await this.saveAndContinue();

    await this.byId('field-reasonandbenefitstrustdevelopmentalneeds-change-link').click();
    await this.byId('Data_reasonAndBenefitsTrustDevelopmentalNeeds').fill('Maintain and improve testing text');
    await this.saveAndContinue();

    await this.byId('field-reasonandbenefitstrustacademiestrustsworkedtogether-change-link').click();
    // Have the academies and trusts worked together in the past? -> Yes
    await this.byId('Data_reasonAndBenefitsTrustAcademiesTrustsWorkedTogether_').check();
    await this.saveAndContinue();
    await this.byId('Data_reasonAndBenefitsTrustHowHaveAcademiesTrustsWorkedTogether').fill(
      'Worked together testing text',
    );
    await this.saveAndContinue();

    await this.markCompleteAndSave();
  }
}
