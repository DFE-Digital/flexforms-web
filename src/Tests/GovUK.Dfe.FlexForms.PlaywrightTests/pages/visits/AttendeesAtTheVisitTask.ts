import { TaskPage } from '../TaskPage';

export class AttendeesAtTheVisitTask extends TaskPage {
  protected readonly taskItem = 'group-engagement-details-task-attendees-at-the-visit';

  async complete(): Promise<void> {
    await this.markCompleteAndSave();
  }

  async addLeadAttendee(name: string, role: string): Promise<void> {
    await this.addAttendee('leadAttendee', name, role);
  }

  async addDfEAttendee(name: string, role: string): Promise<void> {
    await this.addAttendee('dfeAttendee', name, role, 's');
  }

  async addExternalOrganisationAttendee(name: string, role: string): Promise<void> {
    await this.addAttendee('externalOrganisationAttendee', name, role, 's');
  }

  private async addAttendee(section: string, name: string, role: string, addItemAdditional = ''): Promise<void> {
    await this.byId(`${section}${addItemAdditional}-add-item`).click();
    await this.byIdData_(`${section}FirstName`).fill(name.split(' ')[0]);
    await this.byIdData_(`${section}LastName`).fill(name.split(' ')[1]);
    await this.byIdData_(`${section}JobTitle`).fill(role);
    await this.saveAndContinue();
  }
}
