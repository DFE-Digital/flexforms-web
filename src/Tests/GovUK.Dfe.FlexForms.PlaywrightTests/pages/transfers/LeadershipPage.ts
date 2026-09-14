import { TaskPage } from '../TaskPage';

export class LeadershipPage extends TaskPage {
  protected readonly taskItem = 'group-about-the-trust-that-academies-are-joining-task-leadership-and-work-force';

  async complete(): Promise<void> {
    // Will the leadership central team change? -> Yes + text
    await this.byId('field-leadershipandworkforcewilltheleadershipcentralteamchange-change-link').click();
    await this.byId('Data_leadershipAndWorkForceWillTheLeadershipCentralTeamChange_').check();
    await this.saveAndContinue();
    await this.byId('Data_leadershipAndWorkForceHowWillTheLeadershipCentralTeamChange').fill(
      'Leadership and work force testing text',
    );
    await this.saveAndContinue();

    await this.markCompleteAndSave();
  }
}
