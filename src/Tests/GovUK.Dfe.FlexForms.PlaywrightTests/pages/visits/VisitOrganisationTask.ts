import { TaskPage } from '../TaskPage';

export class VisitOrganisationTask extends TaskPage {
  protected readonly taskItem = 'group-engagement-details-task-visit-organisation';

  async complete(): Promise<void> {
    await this.markCompleteAndSave();
  }

  async addTrust(trustName: string): Promise<void> {
    await this.addOrganisationWithSearch(trustName, 'incomingTrustsSearch-field-flow');
  }

  async addSchool(schoolName: string): Promise<void> {
    await this.addOrganisationWithSearch(schoolName, 'academiesSearch', '-2');
  }

  async addLocalAuthority(laName: string): Promise<void> {
    await this.addOrganisationWithSearch(laName, 'localauthoritySearch-field-flow', '-3');
  }
  async addDiocese(dioceseName: string): Promise<void> {
    await this.addOrganisationWithSearch(dioceseName, 'dioceseSearch-field-flow', '-4');
  }

  async addOtherOrganisation(orgName: string): Promise<void> {
    await this.addOrganisation(orgName, 'other-type', '-5');
  }

  async addDfEOrganisedConference(orgName: string): Promise<void> {
    await this.addOrganisation(orgName, 'dfe-conference', '-6');
  }

  async addExternallyOrganisedConference(orgName: string): Promise<void> {
    await this.addOrganisation(orgName, 'external-conference', '-7');
  }

  private async addOrganisationWithSearch(orgName: string, orgSearchField: string, orgNumber = ''): Promise<void> {
    await this.chooseOrganisationType(orgNumber);

    await this.searchAutocomplete(`Data_${orgSearchField}-complex-field`, orgName);
    await this.confirmYesAndContinue();

    await this.byIdData_('visitLocationSelection_-9').check();
    await this.saveAndContinue();
  }

  private async addOrganisation(orgName: string, orgField: string, orgNumber: string): Promise<void> {
    await this.chooseOrganisationType(orgNumber);

    await this.byIdData_(`organisation-${orgField}-field`).fill(orgName);
    await this.saveAndContinue();

    await this.byIdData_('visitLocationSelection_-3').check();
    await this.saveAndContinue();
  }

  private async chooseOrganisationType(orgNumber: string): Promise<void> {
    await this.byId('visitOrganisations-add-item').click();
    await this.byIdData_(`organisationTypeSelection_${orgNumber}`).check();
    await this.saveAndContinue();
  }
}
