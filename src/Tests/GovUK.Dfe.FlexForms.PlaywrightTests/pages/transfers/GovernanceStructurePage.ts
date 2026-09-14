import { TaskPage } from '../TaskPage';

export class GovernanceStructurePage extends TaskPage {
  protected readonly taskItem = 'group-about-the-trust-that-academies-are-joining-task-governance-structure';
  private static readonly uploadField = 'governanceStructureAfterTheTransferPploadDocuments';

  async complete(): Promise<void> {
    // Governance team confirmation -> No + explanation
    await this.byId('field-governanceteamconfirmation-change-link').click();
    await this.byId('Data_governanceTeamConfirmation_-2').check();
    await this.saveAndContinue();
    await this.byId('Data_governanceTeamExplanation').fill('Governance structure testing text');
    await this.saveAndContinue();

    // Proposed governance structure -> upload document
    await this.byId('field-governancestructureafterthetransferpploaddocuments-change-link').click();
    await this.uploadFile(GovernanceStructurePage.uploadField);

    await this.markCompleteAndSave();
  }
}
