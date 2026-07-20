export class Academies{

    static selectors = {

        DetailsofAcademies: 'group-about-transferring-academies-task-details-of-academies',
        AddAnAcademyButton:'detailsOfAcademies-add-item',
        SearchAnAcademy:'Data_academiesSearch-complex-field',
        ClickChangeacademies: 'field-academiessearch',
        searchacademy:'Data_academiesSearch-complex-field',
        RightAcademyYesRadio:'confirmed-yes',
        RightAcademyNoRadio:'confirmed-no',
        SelectedacademyName:'Data_academiesSearch-complex-field-container__option--0',
        SearchButton:'autocomplete-confirm-button',
        verifyselectedAcademy:'govuk-heading-m',
        AcademyFundingYesRadio:'Data_academyFunding_',
        AcademyFundingNoRadio:'Data_academyFunding_-2',
        academyOperationDiffrentlyTextArea:'Data_academyOperatingDifferently',
        DiocesanConsentYesRadio:'Data_detailsOfAcademiesDiocesanConsent_',
        DiocesanConsentNoRadio:'Data_detailsOfAcademiesDiocesanConsent_-2',
        ClickChooseFile:  'upload-file-detailsOfAcademiesUploadConsent',
        filepath:'C:/Users/nsadana/Downloads/Diocesan Consent.xlsx',
        ukprn:'UKPRN: 10017109',
        saveandcontinue:'save-task-summary-button',
        Markcompletecheckbox:'IsTaskCompleted',
        EnterDateAcademyWillJoin:'Data_proposedTransferDate.Day',
        EnterMonthAcademyWillJoin:'Data_proposedTransferDate.Month',
        EnterYearAcademyWillJoin:'Data_proposedTransferDate.Year',
        TaskStatus: 'task-details-of-academies-status',



    }

    static clickDetailsOfAcademies() 
        {

            cy.getById(this.selectors.DetailsofAcademies).contains('Details of academies').click();
        }
     static clickDetailsOfAcademiesIfNotStarted() {
        if (cy.getById(this.selectors.TaskStatus).contains('Not started')) {
            this.clickDetailsOfAcademies();
        }
    }  
      static AddAnAcademy(AcademyName:string, AcademyFunding:string, DiocesanConsent:string){
        cy.getById(this.selectors.AddAnAcademyButton).click();
        this.searchAcademy(AcademyName);
        cy.wait(2000);
        cy.getById(this.selectors.SearchButton).contains('Search').click();
        cy.wait(1000);
        this.verifyselectedAcademy(AcademyName);
        cy.getById(this.selectors.RightAcademyYesRadio).check();
        cy.ClickContinue();
        this.EnterDateAcademyWillJoin('01','12','2024');
        if (AcademyFunding == 'Yes' && DiocesanConsent == 'Yes')
        {
        cy.getById(this.selectors.AcademyFundingYesRadio).check();
        cy.SaveAndContinue();
        cy.getById(this.selectors.DiocesanConsentYesRadio).check();
        cy.SaveAndContinue();
        this.uploadFile();
        }
        else if (AcademyFunding == 'No' && DiocesanConsent == 'No')
        {
        cy.getById(this.selectors.AcademyFundingNoRadio).check();
        cy.SaveAndContinue();
        cy.getById(this.selectors.academyOperationDiffrentlyTextArea).type('Academy will operate differently text area testing');
        cy.SaveAndContinue();
        cy.getById(this.selectors.DiocesanConsentNoRadio).check();
        cy.SaveAndContinue();
        }
        this.verifyAcademyIsAdded(AcademyName);


      }
    
    static EnterDateAcademyWillJoin(Date: string, Month: string, Year: string){
        cy.getById(this.selectors.EnterDateAcademyWillJoin).type(Date);
        cy.getById(this.selectors.EnterMonthAcademyWillJoin).type(Month);
        cy.getById(this.selectors.EnterYearAcademyWillJoin).type(Year);
        cy.SaveAndContinue();
    }




    static clickChangeAcademies()
        {
            cy.getById(this.selectors.ClickChangeacademies).contains('Change').click();
        }
    static searchAcademy(AcademyName:string){
       cy.getById(this.selectors.SearchAnAcademy).click().type(AcademyName);
        cy.getById(this.selectors.SelectedacademyName).contains(AcademyName).click({force: true});
    }
    static verifyselectedAcademy(AcademyName:string) {

        cy.getByClass(this.selectors.verifyselectedAcademy).contains(AcademyName);
    }


    static uploadFile() {

         cy.getById(this.selectors.ClickChooseFile).click();
         cy.attachFixtureFile('input[type="file"]', 'cypress/fixtures/Diocesan Consent.xlsx');
         cy.getByClass('govuk-button').contains('Upload file').click();
      //   cy.attachFixtureFile('input[type="file"]','Finance Documents.docx','Finance Documents.docx','application/vnd.openxmlformats-officedocument.wordprocessingml.document');
      //   cy.getByClass('govuk-button').contains('Upload file').click();
         cy.get('button.govuk-button').contains('Save and continue').click();

        }

        static verifyAcademyIsAdded(AcademyName:string){
            cy.getByClass('govuk-notification-banner__heading').should('contain.text', AcademyName + ' has been added');
        }
    
    static clickMarkCompleteCheckbox() {
        cy.getById(this.selectors.Markcompletecheckbox).check();
        cy.getById(this.selectors.Markcompletecheckbox).should('be.checked');
    }
    static clickSaveAndContinue() {
        cy.getById(this.selectors.saveandcontinue).contains('Save and continue').click();
        return this;
    }

    static verifyTaskStatusIsCompleted() {

        cy.getById(this.selectors.TaskStatus).contains('Completed')

    }  



}
export default Academies;