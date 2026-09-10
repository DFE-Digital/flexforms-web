import { TaskPage } from '../TaskPage';

export class RisksPage extends TaskPage {
  protected readonly taskItem = 'group-about-transferring-academies-task-risks';

  async complete(): Promise<void> {
    // Due diligence
    await this.byId('field-risksduediligence-change-link').click();
    await this.byId('Data_risksDueDiligence').fill('Due diligence testing text');
    await this.saveAndContinue();

    // Pupil numbers -> Yes + upload
    await this.byId('field-riskspupilnumbers-change-link').click();
    await this.byId('Data_risksPupilNumbers_').check();
    await this.saveAndContinue();
    await this.uploadFile('risksUploadPupilNumbers');

    // Type of transfer -> first option, financial deficit -> Yes + forecast upload
    await this.byId('field-riskstransfertype-change-link').click();
    await this.byId('Data_risksTransferType_').check();
    await this.saveAndContinue();
    await this.byId('Data_risksFinancialDeficit_').check();
    await this.saveAndContinue();
    await this.uploadFile('risksFinancialForecast');

    // Other risks -> Yes + summary
    await this.byId('Data_risksOtherRisks_').check();
    await this.saveAndContinue();
    await this.byId('Data_risksRiskManagement').fill('Other risks testing text');
    await this.saveAndContinue();

    // Finances pooled -> GAG pooled (walks through the retained pages)
    await this.byId('field-risksfinancespooled-change-link').click();
    await this.byId('Data_risksFinancesPooled_').check();
    await this.saveAndContinue();
    await this.saveAndContinue();
    await this.saveAndContinue();

    // Surplus funds / reserves transfer
    await this.byId('field-risksreservestransfer-change-link').click();
    await this.byId('Data_risksReservesTransfer').fill('Surplus funds testing text');
    await this.saveAndContinue();

    await this.markCompleteAndSave();
  }
}
