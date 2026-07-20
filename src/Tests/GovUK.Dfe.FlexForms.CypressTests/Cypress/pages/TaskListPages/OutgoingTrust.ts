
export class OutgoingTrust{

static selectors = {

        DetailsofTrusts: 'group-about-the-trusts-that-academies-are-leaving-task-details-of-trusts',
        TaskStatus: 'task-details-of-trusts-status',
        taskCompleted:'task-details-of-trusts-status',
        saveandcontinue:'save-task-summary-button',
        Markcompletecheckbox:'IsTaskCompleted',
        AddaTrustButton:'detailsOfOutgoingTrusts-add-item',
        FindaTrustInput:'Data_trustsSearch-field-flow-complex-field',
        SelectedTrust:'Data_trustsSearch-field-flow-complex-field-container__option--0',
        SearchTrust:'autocomplete-confirm-button',
        TrustName:'govuk-heading-m',
        clickyesbutton:'confirmed-yes',
        clicknobutton:'confirmed-no',
        PageTitle:'',
        TrustClosureYes:'Data_willTrustClose_',
        TrustClosureNo:'Data_willTrustClose_-2',
        ClickChooseFile:'upload-file-outgoingTrustUploadBoardResolution',
        filePath:'C:/Users/nsadana/Downloads/Board Resolution.pdf',
        TrustDetailsSuccesBanner:''

    }

    static clickDetailsofTrust() 
        {
            cy.getById(this.selectors.DetailsofTrusts).contains('Details of trusts').click();
        }

    static clickDetailsofTrustIfNotStarted() {
        if (cy.getById(this.selectors.TaskStatus).contains('Not started')) {
            this.clickDetailsofTrust();
        }
    }
    static AddTrust(trustName: string){
        cy.getById(this.selectors.AddaTrustButton).contains('Add a trust').click();
        this.findTrust(trustName);
        this.ClicksearchTrust();
        this.VerifyTrust(trustName);
        this.confirmselection();
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
        cy.ClickContinue();
     }


      static EnterContactDetails( name: string, role: string, phoneNumber: string,Email: string) {
        cy.getById('Data_outgoingTrustContactDetailsFullName').type(name);
        cy.getById('Data_outgoingTrustContactDetailsRole').type(role);
        cy.getById('Data_outgoingTrustContactDetailsPhoneNumber').type(phoneNumber);
        cy.getById('Data_outgoingTrustContactDetailsEmailAddress').type(Email)
        cy.SaveAndContinue();
    
     }
     static SelectTrustClosure(isTrustClosing?: string){
        if (isTrustClosing =='Yes'){
        cy.getById(this.selectors.TrustClosureYes).check();

        }
        else if (isTrustClosing =='No'){
            cy.getById(this.selectors.TrustClosureNo).check();
        }
        cy.SaveAndContinue();
        this.uploadFile();

        }
     

        static uploadFile() {

            cy.getById(this.selectors.ClickChooseFile).click();
            cy.get('input[type="file"]').selectFile(this.selectors.filePath);
            cy.getByClass('govuk-button').contains('Upload file').click();
            cy.get('button.govuk-button').contains('Save and continue').click();
        }

       static verifyOutgoingTrustIsAdded() {

      //  cy.getByClass(this.selectors.TrustDetailsSuccesBanner).contains('The trust that academies are joining has been added');

       }  


    static verifyTaskStatusIsCompleted() {

        cy.getById(this.selectors.TaskStatus).contains('Completed')

    }  
    
    static clickMarkCompleteCheckbox() {
        cy.getById(this.selectors.Markcompletecheckbox).check();
        cy.getById(this.selectors.Markcompletecheckbox).should('be.checked');
    }
    
    static clickSaveAndContinue() {
        cy.getById(this.selectors.saveandcontinue).contains('Save and continue').click();
        return this;
    }



}
    




export default OutgoingTrust;




