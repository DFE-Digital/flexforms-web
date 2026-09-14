import { TaskPage } from '../TaskPage';

export class FinanceAndOperationsPage extends TaskPage {
  protected readonly taskItem = 'group-about-the-trust-that-academies-are-joining-task-finance-and-operations';
  private static readonly uploadField = 'financeAndOperationsUploadGrowthPlanNext3Years';

  async complete(): Promise<void> {
    // Growth plan -> Yes, then upload
    await this.byId('field-financeandoperationshavegrowthplannext3years-change-link').click();
    await this.byId('Data_financeAndOperationsHaveGrowthPlanNext3Years_').check();
    await this.saveAndContinue();
    await this.uploadFile(FinanceAndOperationsPage.uploadField);

    // Policy on charges made to academies -> Yes + text
    await this.byId('field-financeandoperationspolicyonchargesmadetoitsacademies-change-link').click();
    await this.byId('Data_financeAndOperationsPolicyOnChargesMadeToItsAcademies_').check();
    await this.saveAndContinue();
    await this.byId('Data_financeAndOperationsHowWillPolicyOnChargesMadeToItsAcademies').fill(
      'Charge on academies testing text',
    );
    await this.saveAndContinue();

    // Service level agreements -> Yes / Yes + text
    await this.byId('field-financeandoperationshavesapacademies-change-link').click();
    await this.byId('Data_financeAndOperationsHaveSAPAcademies_').check();
    await this.saveAndContinue();
    await this.byId('Data_financeAndOperationsLocalAuthorityAgreements_').check();
    await this.saveAndContinue();
    await this.byId('Data_financeAndOperationsSummariseTheAgreements').fill('Alternative agreements testing text');
    await this.saveAndContinue();

    await this.markCompleteAndSave();
  }
}
