import { expect, type Locator, type Page } from '@playwright/test';
import type { Terminology } from '../support/types';
import { BasePage } from './BasePage';
import { ApplicationsTable } from './components/ApplicationsTable';
import { requireEnvironmentVariable } from '../support/environment';

export class DashboardPage extends BasePage {
  readonly applicationsTable: ApplicationsTable;

  constructor(page: Page, terminology: Terminology) {
    super(page, terminology);
    this.applicationsTable = new ApplicationsTable(page);
  }

  async chooseDefaultForm(): Promise<void> {
    await this.byId(`form-${requireEnvironmentVariable('TEMPLATE_ID')}`).click();
    await this.page.getByRole('button', { name: 'Go to dashboard' }).click();
  }

  async startNewApplication(): Promise<void> {
    await this.startNewApplicationButton().click();
  }

  async filterApplications(): Promise<void> {
    await this.filterApplicationsButton().click();
  }

  async applyFilters(): Promise<void> {
    await this.applyFilterButton().click();
  }

  async filterApplicationsByReference(reference: string): Promise<void> {
    await this.filterReferenceInput().fill(reference);
  }

  async expectApplicationPresent(reference: string): Promise<void> {
    await expect(this.applicationLink(reference)).toBeVisible();
  }

  async expectApplicationNotPresent(reference: string): Promise<void> {
    await expect(this.applicationLink(reference)).toHaveCount(0);
  }

  private startNewApplicationButton(): Locator {
    return this.byId('start-new-application-button');
  }

  private filterApplicationsButton(): Locator {
    return this.page.getByTestId('filter-applications-button');
  }

  private applyFilterButton(): Locator {
    return this.page.getByRole('button', { name: 'Apply filters' });
  }

  private filterReferenceInput(): Locator {
    return this.page.getByLabel('Reference number');
  }

  private applicationLink(reference: string): Locator {
    return this.page.getByRole('link', { name: reference });
  }
}
