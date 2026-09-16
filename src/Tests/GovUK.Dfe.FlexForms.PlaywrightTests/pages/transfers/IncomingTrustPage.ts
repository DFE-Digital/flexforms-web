import { TaskPage } from '../TaskPage';

interface ContactDetails {
  name: string;
  phone: string;
  email: string;
}

export class IncomingTrustPage extends TaskPage {
  protected readonly taskItem = 'group-about-the-trust-that-academies-are-joining-task-trust-details';
  private static readonly searchInput = 'Data_incomingTrustsSearch-field-flow-complex-field';
  private static readonly uploadField = 'incomingTrustUploadBoardResolution';

  async complete(trustName: string): Promise<void> {
    await this.byId('detailsOfIncomingTrust-add-item').click();

    await this.searchAutocomplete(IncomingTrustPage.searchInput, trustName);
    await this.confirmYesAndContinue();

    // What is the type of trust? -> Single academy trust (first option)
    await this.byId('Data_incomingTrustTypeOfTrust_').check();
    await this.saveAndContinue();

    await this.enterContact('incomingTrustAccountingOfficer', {
      name: 'Test Officer',
      phone: '0123456789',
      email: 'officer@gov.uk',
    });
    await this.enterContact('incomingTrustChiefFinancialOfficer', {
      name: 'Finance Officer',
      phone: '0987654321',
      email: 'finance@gov.uk',
    });
    await this.enterContact('incomingTrustChairOfTrustee', {
      name: 'Chair Trustee',
      phone: '0987654321',
      email: 'chair@gov.uk',
    });

    // Main contact has an additional Role field
    await this.byId('Data_incomingTrustMainContactFullName').fill('Main Contact');
    await this.byId('Data_incomingTrustMainContactRole').fill('Director');
    await this.byId('Data_incomingTrustMainContactPhoneNumber').fill('0123456789');
    await this.byId('Data_incomingTrustMainContactEmailAddress').fill('main@gov.uk');
    await this.saveAndContinue();

    await this.uploadFile(IncomingTrustPage.uploadField);

    await this.markCompleteAndSave();
  }

  private async enterContact(fieldPrefix: string, contact: ContactDetails): Promise<void> {
    await this.byId(`Data_${fieldPrefix}FullName`).fill(contact.name);
    await this.byId(`Data_${fieldPrefix}PhoneNumber`).fill(contact.phone);
    await this.byId(`Data_${fieldPrefix}EmailAddress`).fill(contact.email);
    await this.saveAndContinue();
  }
}
