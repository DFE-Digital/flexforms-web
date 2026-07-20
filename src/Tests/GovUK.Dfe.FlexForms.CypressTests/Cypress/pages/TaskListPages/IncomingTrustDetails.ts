
export class IncomingTrust{

static selectors = {

        DetailsofTrusts: 'group-about-the-trust-that-academies-are-joining-task-trust-details',
        TaskStatus: 'task-trust-details-status',
        saveandcontinue:'save-task-summary-button',
        Markcompletecheckbox:'IsTaskCompleted',
        ErrorSummary:'govuk-error-summary',
        AddaTrustButton:'detailsOfIncomingTrust-add-item',
        FindaTrustInput:'Data_incomingTrustsSearch-field-flow-complex-field',
        SearchTrust:'autocomplete-confirm-button',
        TrustName:'govuk-inset-text',
        clickyesbutton:'confirmed-yes',
        clickContinue:'confirmation-continue',
        PageTitle:'page-title',
        singleAcademyTrust:'Data_incomingTrustTypeOfTrust_',
        accountingOfficerName:"Head of finance's details",
        ClickChooseFile:'upload-file-incomingTrustUploadBoardResolution',
        filePath: '.../src/Tests/GovUK.Dfe.FlexForms.CypressTests/cypress/fixtures/Board Resolution.pdf',
        uploadFile:'',
        TrustDetailsSuccesBanner:'govuk-notification-banner__content',
        SelectedTrust:'Data_incomingTrustsSearch-field-flow-complex-field-container__option--0'

        }

    static clickDetailsofTrust() 
        {
            cy.getById(this.selectors.DetailsofTrusts).contains('Trust details').click();
        }

    static clickDetailsofTrustIfNotStarted() {
        if (cy.getById(this.selectors.TaskStatus).contains('Not started')) {
            this.clickDetailsofTrust();
        }
    }
     static clickMarkCompleteCheckbox() {
        cy.getById(this.selectors.Markcompletecheckbox).check();
        cy.getById(this.selectors.Markcompletecheckbox).should('be.checked');
    }

    static verifyTaskStatusIsNotCompleted() {

        cy.getByClass(this.selectors.ErrorSummary).contains('There is a problem')

    }  

    static AddTrust(){
        cy.getById(this.selectors.AddaTrustButton).contains('Add a trust').click();
    }
     static findTrust(trustName: string){
        cy.getById(this.selectors.FindaTrustInput).click().type(trustName);
        cy.getById(this.selectors.SelectedTrust).contains(trustName).click({force: true});
     }


     static ClicksearchTrust(){
        cy.getById(this.selectors.SearchTrust).contains('Search').click();
     }

     static VerifyTrust(trustName: string) {

        cy.getByClass(this.selectors.TrustName).contains(trustName);
     }

     static confirmselection() {

        cy.getById(this.selectors.clickyesbutton).should('not.be.checked');
        cy.getById(this.selectors.clickyesbutton).check();
     }

     static continueButton() {

        cy.getById(this.selectors.clickContinue).contains('Continue').click();
     }

     static SelectTypeOfTrust(){
        cy.getById(this.selectors.PageTitle).contains('What is the type of trust?');
        cy.getById(this.selectors.singleAcademyTrust).should('not.be.checked');
        cy.getById(this.selectors.singleAcademyTrust).check();
        cy.SaveAndContinue();

     }
      
     static EnterAccountingOfficerDetails( name: string,  phoneNumber: string,officerEmail: string,) {
        cy.getById('Data_incomingTrustAccountingOfficerFullName').type(name);
        cy.getById('Data_incomingTrustAccountingOfficerPhoneNumber').type(phoneNumber);
        cy.getById('Data_incomingTrustAccountingOfficerEmailAddress').type(officerEmail);
        cy.SaveAndContinue();
    
     }
     
     static EnterHeadofFinanceDetails( name: string,  phoneNumber: string,officerEmail: string,) {
        cy.getById('Data_incomingTrustChiefFinancialOfficerFullName').type(name);
        cy.getById('Data_incomingTrustChiefFinancialOfficerPhoneNumber').type(phoneNumber);
        cy.getById('Data_incomingTrustChiefFinancialOfficerEmailAddress').type(officerEmail);
        cy.SaveAndContinue();
    
     }

     static EnterChairofTrusteesDetails( name: string,  phoneNumber: string,officerEmail: string,) {

        cy.getById('Data_incomingTrustChairOfTrusteeFullName').type(name);
        cy.getById('Data_incomingTrustChairOfTrusteePhoneNumber').type(phoneNumber);
        cy.getById('Data_incomingTrustChairOfTrusteeEmailAddress').type(officerEmail);
        cy.SaveAndContinue();
    
     }

        static EnterDetailsofMainContact( name: string, role:string, phoneNumber: string,officerEmail: string,) {
        cy.getById('Data_incomingTrustMainContactFullName').type(name);
        cy.getById('Data_incomingTrustMainContactRole').type(role);
        cy.getById('Data_incomingTrustMainContactPhoneNumber').type(phoneNumber);
        cy.getById('Data_incomingTrustMainContactEmailAddress').type(officerEmail);
        cy.SaveAndContinue();
        } 
        static uploadFile() {

            
            cy.getById(this.selectors.ClickChooseFile).click();    
            cy.get('input[type="file"]').selectFile('cypress/fixtures/Diocesan Consent.xlsx');
            cy.getByClass('govuk-button').contains('Upload file').click();
            cy.get('button.govuk-button').contains('Save and continue').click();
        }
    static UploadResolution(UploadResolution: any) {
        throw new Error("Method not implemented.");
    }

       static verifyFileUploadSuccessMessage() {

        cy.getByClass(this.selectors.TrustDetailsSuccesBanner).contains('The trust that academies are joining has been added');

       }


    static verifyTaskStatusIsCompleted() {
        
        cy.getById(this.selectors.TaskStatus).contains('Completed')
    }  
    
  
    
    static clickSaveAndContinue() {
        cy.getById(this.selectors.saveandcontinue).contains('Save and continue').click();
        return this;
    }



}
    




export default IncomingTrust;




