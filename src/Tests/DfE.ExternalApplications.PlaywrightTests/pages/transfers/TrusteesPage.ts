import { TaskPage } from '../TaskPage';

export class TrusteesPage extends TaskPage {
  protected readonly taskItem = 'group-about-the-trust-that-academies-are-joining-task-trustees';

  async addExistingTrustee(name: string, futureRoles: string, localGoverningBody: boolean): Promise<void> {
    await this.byId('trusteesAfterTransfer-add-item').click();
    await this.saveAndContinue();
    await this.byId('Data_trusteeName').first().fill(name);
    await this.saveAndContinue();
    await this.byId('Data_existingTrustee_').first().check();
    await this.saveAndContinue();
    await this.byId('Data_trusteeFutureRoles').fill(futureRoles);
    await this.saveAndContinue();
    await this.byId(
      localGoverningBody ? 'Data_trusteeLocalGoverningBody_' : 'Data_trusteeLocalGoverningBody_-2',
    ).check();
    await this.saveAndContinue();
  }

  async addNewTrustee(
    name: string,
    pastRoles: string,
    localGoverningBody: boolean,
    futureRoles: string,
  ): Promise<void> {
    await this.byId('trusteesAfterTransfer-add-item').click();
    await this.saveAndContinue();
    await this.byId('Data_trusteeName').first().fill(name);
    await this.saveAndContinue();
    await this.byId('Data_existingTrustee_-2').first().check();
    await this.saveAndContinue();
    await this.byId('Data_trusteePastRoles').fill(pastRoles);
    await this.saveAndContinue();
    await this.byId('Data_trusteeFutureRoles').fill(futureRoles);
    await this.saveAndContinue();
    await this.byId(
      localGoverningBody ? 'Data_trusteeLocalGoverningBody_' : 'Data_trusteeLocalGoverningBody_-2',
    ).check();
    await this.saveAndContinue();
  }

  async addLeavingTrustee(name: string): Promise<void> {
    await this.byId('trusteesLeaving-add-item').click();
    await this.byId('Data_trusteeLeavingName').first().fill(name);
    await this.saveAndContinue();
  }

  async complete(): Promise<void> {
    await this.markCompleteAndSave();
  }
}
