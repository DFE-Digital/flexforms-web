import { TaskPage } from '../TaskPage';

export class DateOfTheVisitTask extends TaskPage {
  protected readonly taskItem = 'group-engagement-details-task-date-of-the-visit';

  async complete(): Promise<void> {
    await this.byId('field-datevisited-change-link').click();
    await this.enterDate('Data_dateVisited', '01', '01', '2024');
    await this.saveAndContinue();

    await this.markCompleteAndSave();
  }
}
