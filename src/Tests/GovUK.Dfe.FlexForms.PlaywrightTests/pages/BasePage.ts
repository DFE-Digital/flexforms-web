import type { Locator, Page } from '@playwright/test';
import type { Terminology } from '../support/types';

export abstract class BasePage {
  protected readonly page: Page;
  protected readonly terminology: Terminology;

  constructor(page: Page, terminology: Terminology) {
    this.page = page;
    this.terminology = terminology;
  }

  protected byId(id: string): Locator {
    return this.page.locator(`[id="${id}"]`);
  }
}
