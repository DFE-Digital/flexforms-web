import { TaskPage } from '../TaskPage';

export class SchoolImprovementPage extends TaskPage {
  protected readonly taskItem = 'group-about-the-trust-that-academies-are-joining-task-school-improvement';
  private static readonly uploadField = 'schoolImprovementModel';

  async complete(): Promise<void> {
    await this.byId('field-schoolimprovementmodel-change-link').click();
    await this.uploadFile(SchoolImprovementPage.uploadField);
    await this.markCompleteAndSave();
  }
}
