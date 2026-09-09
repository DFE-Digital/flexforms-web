import fs from 'node:fs';
import path from 'node:path';
import { expect, Locator, Page } from '@playwright/test';

export const UPLOAD_FIXTURE = path.resolve(__dirname, '../assets/upload.pdf');

export abstract class FormPage {
  constructor(protected readonly page: Page) {}

  protected byId(id: string): Locator {
    return this.page.locator(`[id="${id}"]`);
  }

  protected async saveAndContinue(): Promise<void> {
    await this.byId('save-and-continue-button').click();
  }

  protected async markCompleteAndSave(): Promise<void> {
    await this.byId('IsTaskCompleted').check();
    await this.byId('save-task-summary-button').click();
    await expect(this.page).toHaveURL(/\/applications\/[^/]+$/);
  }

  protected async confirmContinue(): Promise<void> {
    await this.byId('confirmation-continue').click();
  }

  protected async confirmYesAndContinue(): Promise<void> {
    await this.byId('confirmed-yes').check();
    await this.confirmContinue();
  }

  protected async searchAutocomplete(inputId: string, searchText: string): Promise<void> {
    const input = this.byId(inputId);
    await input.click();
    await input.pressSequentially(searchText, { delay: 50 });
    await this.byId(`${inputId}-container__option--0`).click();
    await this.byId('autocomplete-confirm-button').click();
  }

  protected async uploadFile(fieldId: string, filePath = UPLOAD_FIXTURE): Promise<void> {
    // each upload needs a unique name
    const extension = path.extname(filePath);
    const baseName = path.basename(filePath, extension);
    const fileName = `${baseName}-${fieldId}${extension}`;

    await this.byId(`upload-file-${fieldId}`).setInputFiles({
      name: fileName,
      mimeType: extension.toLowerCase() === '.pdf' ? 'application/pdf' : 'application/octet-stream',
      buffer: fs.readFileSync(filePath),
    });
    await this.byId(`submit-upload-file-${fieldId}`).click();
    await expect(this.byId(`download-${fileName}`)).toContainText(fileName, { timeout: 15_000 });
    await this.byId(`submit-${fieldId}`).click();
  }

  protected async enterDate(prefix: string, day: string, month: string, year: string): Promise<void> {
    await this.byId(`${prefix}.Day`).fill(day);
    await this.byId(`${prefix}.Month`).fill(month);
    await this.byId(`${prefix}.Year`).fill(year);
  }
}
