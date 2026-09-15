import { expect, type Locator, type Page } from '@playwright/test';

/**
 * Fluent GOV.UK table assertions for the applications listing.
 * Chain methods then await verify() so Playwright retries still apply.
 */
export class ApplicationsTable {
  private readonly page: Page;
  private readonly table: Locator;
  private reference = '';
  private assertions: Promise<void> = Promise.resolve();

  constructor(page: Page) {
    this.page = page;
    this.table = page.getByRole('table');
  }

  withReference(reference: string): this {
    this.reference = reference;
    return this;
  }

  hasTableHeaders(headers: string[]): this {
    this.enqueue(async () => {
      const headerCells = this.headerCells();
      await expect(headerCells).toHaveCount(headers.length);
      for (let i = 0; i < headers.length; i++) {
        await expect(headerCells.nth(i)).toHaveText(headers[i]);
      }
    });
    return this;
  }

  hasNumberOfRows(expected: number): this {
    this.enqueue(async () => {
      await expect(this.bodyRows()).toHaveCount(expected);
    });
    return this;
  }

  columnHasValue(tableColumn: string, expectedValue: string): this {
    this.enqueue(async () => {
      const cell = await this.cellForColumn(tableColumn);
      await expect(cell).toHaveText(expectedValue);
    });
    return this;
  }

  columnHasValueWithLink(tableColumn: string, expectedValue: string, href: string): this {
    this.enqueue(async () => {
      const cell = await this.cellForColumn(tableColumn);
      const link = this.cellLink(cell);
      await expect(link).toContainText(expectedValue);
      await expect(link).toHaveAttribute('href', href);
    });
    return this;
  }

  async verify(): Promise<void> {
    await this.assertions;
  }

  private enqueue(assertion: () => Promise<void>): void {
    this.assertions = this.assertions.then(assertion);
  }

  private headerCells(): Locator {
    return this.table.getByRole('columnheader');
  }

  private bodyRows(): Locator {
    return this.table.getByRole('row').filter({ has: this.page.getByRole('cell') });
  }

  private cellLink(cell: Locator): Locator {
    return cell.getByRole('link');
  }

  private async cellForColumn(tableColumn: string): Promise<Locator> {
    if (!this.reference) {
      throw new Error('Reference is not set. Call withReference() before asserting a table cell value.');
    }

    const headerCells = this.headerCells();
    await expect(headerCells.filter({ hasText: tableColumn })).toHaveCount(1);

    const columnIndex = await headerCells.evaluateAll((headers, column) => {
      return headers.findIndex((header) => header.textContent?.trim() === column);
    }, tableColumn);

    if (columnIndex < 0) {
      throw new Error(`Table column "${tableColumn}" was not found.`);
    }

    const row = this.bodyRows().filter({
      has: this.page.getByRole('cell', { name: this.reference, exact: true }),
    });

    return row.getByRole('cell').nth(columnIndex);
  }
}

export function formatApplicationDisplayDate(date: Date = new Date()): string {
  return new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(date);
}
