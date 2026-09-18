import { TaskPage } from '../TaskPage';

export class UploadYourLSRPDataTemplateTask extends TaskPage {
  protected readonly taskItem = 'group-financial-summary-task-upload-your-local-send-reform-plan-data-template';

  async complete(): Promise<void> {
    await this.uploadFile('LocalSENDReformPlanDataTemplate');
    await this.markCompleteAndSave();
  }
}
