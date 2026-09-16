import { TaskPage } from '../TaskPage';

export class DetailsOfAcademiesPage extends TaskPage {
  protected readonly taskItem = 'group-about-transferring-academies-task-details-of-academies';
  private static readonly searchInput = 'Data_academiesSearch-complex-field';

  async complete(academyName: string): Promise<void> {
    await this.byId('detailsOfAcademies-add-item').click();

    await this.searchAutocomplete(DetailsOfAcademiesPage.searchInput, academyName);
    await this.confirmYesAndContinue();

    await this.enterDate('Data_proposedTransferDate', '01', '12', '2024');
    await this.saveAndContinue();

    // Does the academy receive additional funding? -> No
    await this.byId('Data_academyFunding_-2').check();
    await this.saveAndContinue();
    await this.byId('Data_academyOperatingDifferently').fill('Academy will operate differently testing text');
    await this.saveAndContinue();

    // Diocesan consent required? -> No
    await this.byId('Data_detailsOfAcademiesDiocesanConsent_-2').check();
    await this.saveAndContinue();

    await this.markCompleteAndSave();
  }
}
