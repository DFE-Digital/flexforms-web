import { expect } from '@playwright/test';
import { TaskPage } from '../TaskPage';

export class OutgoingTrustPage extends TaskPage {
  protected readonly taskItem = 'group-about-the-trusts-that-academies-are-leaving-task-details-of-trusts';
  private static readonly searchInput = 'Data_trustsSearch-field-flow-complex-field';
  private static readonly uploadField = 'outgoingTrustUploadBoardResolution';

  private async expectSummary(): Promise<void> {
    await expect(this.page).toHaveURL(/\/details-of-outgoing-trusts$/);
    await expect(this.byId('IsTaskCompleted')).toBeVisible();
  }

  async complete(trustName: string): Promise<void> {
    await this.byId('detailsOfOutgoingTrusts-add-item').click();

    await this.searchAutocomplete(OutgoingTrustPage.searchInput, trustName);
    await this.confirmYesAndContinue();

    await this.byId('Data_outgoingTrustContactDetailsFullName').fill('Michael Scott');
    await this.byId('Data_outgoingTrustContactDetailsRole').fill('Granting Officer');
    await this.byId('Data_outgoingTrustContactDetailsPhoneNumber').fill('07700 900 982');
    await this.byId('Data_outgoingTrustContactDetailsEmailAddress').fill('M.A@gov.uk');
    await this.saveAndContinue();

    // Will the trust close? -> Yes + upload board resolution
    await this.byId('Data_willTrustClose_').check();
    await this.saveAndContinue();
    await this.uploadFile(OutgoingTrustPage.uploadField);
    await this.expectSummary();

    await this.markCompleteAndSave();
  }
}
