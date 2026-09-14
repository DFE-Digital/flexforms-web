import { TaskPage } from '../TaskPage';

export class HighQualityInclusiveEducationPage extends TaskPage {
  protected readonly taskItem =
    'group-about-the-trust-that-academies-are-joining-task-high-quality-and-inclusive-education';

  async complete(): Promise<void> {
    await this.byId('field-highqualityandinclusiveeducationquality-change-link').click();
    await this.byId('Data_highQualityAndInclusiveEducationQuality').fill(
      'High quality and inclusive education quality testing text',
    );
    await this.saveAndContinue();

    await this.byId('field-highqualityandinclusiveeducationimpact-change-link').click();
    await this.byId('Data_highQualityAndInclusiveEducationImpact').fill(
      'High quality and inclusive education impact testing text',
    );
    await this.saveAndContinue();

    await this.markCompleteAndSave();
  }
}
