import fs from 'node:fs';
import path from 'node:path';
import { expect, Locator, Page } from '@playwright/test';

export const UPLOAD_FIXTURE = path.resolve(__dirname, '../assets/upload.pdf');

export abstract class FormPage {
  constructor(protected readonly page: Page) {}
  protected byIdData_(id: string): Locator {
    return this.byId(`Data_${id}`);
  }

  protected byId(id: string): Locator {
    return this.page.locator(`[id="${id}"]`);
  }

  protected async saveAndContinue(): Promise<void> {
    await this.saveAndContinueButton().click();
  }

  protected async markCompleteAndSave(): Promise<void> {
    await this.taskCompletedCheckbox().check();
    await this.saveTaskSummaryButton().click();
    await expect(this.page).toHaveURL(/\/applications\/[^/]+$/);
  }

  protected async confirmContinue(): Promise<void> {
    await this.confirmationContinueButton().click();
  }

  protected async confirmYesAndContinue(): Promise<void> {
    await this.confirmedYesRadio().check();
    await this.confirmContinue();
  }

  protected async searchAutocomplete(inputId: string, searchText: string): Promise<void> {
    const input = this.autocompleteInput(inputId);
    await input.click();
    await input.pressSequentially(searchText, { delay: 50 });
    await this.autocompleteFirstOption(inputId).click();
    await this.autocompleteConfirmButton().click();
  }

  protected async uploadFile(fieldId: string, filePath = UPLOAD_FIXTURE): Promise<void> {
    // each upload needs a unique name
    const extension = path.extname(filePath);
    const baseName = path.basename(filePath, extension);
    const fileName = `${baseName}-${fieldId}${extension}`;

    await this.uploadFileInput(fieldId).setInputFiles({
      name: fileName,
      mimeType: extension.toLowerCase() === '.pdf' ? 'application/pdf' : 'application/octet-stream',
      buffer: fs.readFileSync(filePath),
    });
    await this.submitUploadButton(fieldId).click();
    await expect(this.downloadLink(fileName)).toContainText(fileName, { timeout: 15_000 });
    await this.submitFieldButton(fieldId).click();
  }

  protected async enterDate(prefix: string, day: string, month: string, year: string): Promise<void> {
    await this.dateDayInput(prefix).fill(day);
    await this.dateMonthInput(prefix).fill(month);
    await this.dateYearInput(prefix).fill(year);
  }

  private saveAndContinueButton(): Locator {
    return this.byId('save-and-continue-button');
  }

  private taskCompletedCheckbox(): Locator {
    return this.page.getByLabel("Mark this section as complete, it's ready for review");
  }

  private saveTaskSummaryButton(): Locator {
    return this.byId('save-task-summary-button');
  }

  private confirmationContinueButton(): Locator {
    return this.page.getByRole('button', { name: 'Continue' });
  }

  private confirmedYesRadio(): Locator {
    return this.page.getByRole('radio', { name: 'Yes' });
  }

  private autocompleteInput(inputId: string): Locator {
    return this.byId(inputId);
  }

  private autocompleteFirstOption(inputId: string): Locator {
    return this.byId(`${inputId}-container__option--0`);
  }

  private autocompleteConfirmButton(): Locator {
    return this.byId('autocomplete-confirm-button');
  }

  private uploadFileInput(fieldId: string): Locator {
    return this.byId(`upload-file-${fieldId}`);
  }

  private submitUploadButton(fieldId: string): Locator {
    return this.byId(`submit-upload-file-${fieldId}`);
  }

  private downloadLink(fileName: string): Locator {
    return this.byId(`download-${fileName}`);
  }

  private submitFieldButton(fieldId: string): Locator {
    return this.byId(`submit-${fieldId}`);
  }

  private dateDayInput(prefix: string): Locator {
    return this.byId(`${prefix}.Day`);
  }

  private dateMonthInput(prefix: string): Locator {
    return this.byId(`${prefix}.Month`);
  }

  private dateYearInput(prefix: string): Locator {
    return this.byId(`${prefix}.Year`);
  }
}
