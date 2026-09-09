import type { Locator, Page } from '@playwright/test';
import type { Terminology } from '../support/types';
import { BasePage } from './BasePage';
import { ApplicationsTable } from './components/ApplicationsTable';

export class DashboardPage extends BasePage {
  readonly applicationsTable: ApplicationsTable;

  constructor(page: Page, terminology: Terminology) {
    super(page, terminology);
    this.applicationsTable = new ApplicationsTable(page);
  }

  async startNewApplication(): Promise<void> {
    await this.startNewApplicationButton().click();
  }

  async filterApplications() {
    await this.filterApplicationsButton().click();
  }

  async applyFilters(): Promise<void> {
    await this.applyFilterButton().click();
  }

  async filterApplicationsByReference(reference: string): Promise<void> {
    await this.filterReferenceInput().fill(reference);
  }

  private startNewApplicationButton(): Locator {
    return this.byId('start-new-application-button');
  }

  private filterApplicationsButton(): Locator {
    return this.page.getByTestId('filter-applications-button');
  }

  private applyFilterButton(): Locator {
    return this.page.getByTestId('apply-filters');
  }

  private filterReferenceInput(): Locator {
    return this.byId('search-reference');
  }
}
