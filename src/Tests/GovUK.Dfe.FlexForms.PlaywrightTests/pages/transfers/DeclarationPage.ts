import { TaskPage } from '../TaskPage';

export class DeclarationPage extends TaskPage {
  protected readonly taskItem = 'group-declaration-task-declaration-from-all-chairs-of-trustees';

  async complete(): Promise<void> {
    // Joining academy declaration
    await this.page.locator('a[href*="trust-declarations-joining"]').first().click();
    await this.byId('Data_equalities-duties-decision_').check();
    await this.byId('Data_chairName-joining').fill('John Cena');
    await this.enterDate('Data_dateSigned-joining', '11', '11', '2025');
    await this.saveAndContinue();

    // Leaving academy declaration
    await this.page.locator('a[href*="trust-declarations-leaving"]').first().click();
    await this.byId('Data_equalities-duties-decision-leaving_-2').check();
    await this.byId('Data_chairName-leaving').fill('Michelle Loner');
    await this.enterDate('Data_dateSigned-leaving', '20', '01', '2026');
    await this.saveAndContinue();

    await this.markCompleteAndSave();
  }
}
