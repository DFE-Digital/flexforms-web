
export class FinanceAndOperations{

static selectors = {

        FinanceAndOperations: 'group-about-the-trust-that-academies-are-joining-task-finance-and-operations',
        TaskStatus: 'task-finance-and-operations-status',
        GrowthPlanChangeLink:'field-financeandoperationshavegrowthplannext3years-change-link',
        GrowthPlanRadioYes:'Data_financeAndOperationsHaveGrowthPlanNext3Years_',
        GrowthPlanRadioNo:'Data_financeAndOperationsHaveGrowthPlanNext3Years_-2',
        ClickChooseFile: 'upload-file-financeAndOperationsUploadGrowthPlanNext3Years',
        filePath: 'cypress/fixtures/FinanceAndOperation.csv',
        ChargeOnAcademiesChangeLink: 'field-financeandoperationspolicyonchargesmadetoitsacademies-change-link',
        ChargeonAcademiesYesRadio:'Data_financeAndOperationsPolicyOnChargesMadeToItsAcademies_',
        ChargeonAcademiesNoRadio:'Data_financeAndOperationsPolicyOnChargesMadeToItsAcademies_-2',
        ChargeOnAcademiesTextArea:'Data_financeAndOperationsHowWillPolicyOnChargesMadeToItsAcademies',
        AlternativeAcademiesChangeLink:'field-financeandoperationshavesapacademies-change-link',
        AlternativeAcademiesYesRadio:'Data_financeAndOperationsHaveSAPAcademies_',
        AlternativeAcademiesNoRadio:'Data_financeAndOperationsHaveSAPAcademies_-2',
        AlternativeLOcalAuthorityAgreementYes:'Data_financeAndOperationsLocalAuthorityAgreements_',
        AlternativeLOcalAuthorityAgreementNo:'Data_financeAndOperationsLocalAuthorityAgreements_-2',
        AlternativeAgreementsTextArea:'Data_financeAndOperationsSummariseTheAgreements',




    }

    static clickFinanceAndOperations(    ) 
        {
            cy.getById(this.selectors.FinanceAndOperations).contains('Finance and operations').click();
            cy.getById(this.selectors.GrowthPlanChangeLink).click();

        }



    static clickChangeFinanceAndOperationsGrowthPlanOption( growthPlanOption: string) {
        if (growthPlanOption === 'Yes') 
        {
            cy.getById(this.selectors.GrowthPlanRadioYes).check();
            cy.SaveAndContinue();
            this.uploadFile();

        }
    }   

        
    static ClickChangeChargeOnAcademies( chargeOnAcademiesOption: string, chargeOnAcademiesText: string) {
        cy.getById(this.selectors.ChargeOnAcademiesChangeLink).click();
        if (chargeOnAcademiesOption == 'Yes') 
        {
            cy.getById(this.selectors.ChargeonAcademiesYesRadio).check();
            cy.SaveAndContinue();
            cy.getById(this.selectors.ChargeOnAcademiesTextArea).type(chargeOnAcademiesText);
        }
        else if (chargeOnAcademiesOption == 'No') 
        {
            cy.getById(this.selectors.ChargeonAcademiesNoRadio).check();
        }
        cy.SaveAndContinue();

    }

    static ClickChangeAlternativeAcademies(AlternativeAcademiesOption: string, alternativeLOcalAuthorityAgreementOption: string,AlternativeAgreementsText: string){
    
        cy.getById(this.selectors.AlternativeAcademiesChangeLink).click();
        if (AlternativeAcademiesOption == 'Yes' && alternativeLOcalAuthorityAgreementOption == 'Yes'){
            cy.getById(this.selectors.AlternativeAcademiesYesRadio).check();
            cy.SaveAndContinue();
            cy.getById(this.selectors.AlternativeLOcalAuthorityAgreementYes).check();
            cy.SaveAndContinue();
            cy.getById(this.selectors.AlternativeAgreementsTextArea).type(AlternativeAgreementsText);
        }
        else if (AlternativeAcademiesOption == 'No' && alternativeLOcalAuthorityAgreementOption == 'No'){
            cy.getById(this.selectors.AlternativeAcademiesNoRadio).check();
            cy.SaveAndContinue();
            cy.getById(this.selectors.AlternativeLOcalAuthorityAgreementNo).check();
        }
        cy.SaveAndContinue();

        

    }


 static uploadFile() {

    cy.getById(this.selectors.ClickChooseFile).click();
    cy.attachFixtureFile('input[type="file"]', 'Finance.pdf', 'Finance.pdf', 'application/pdf');
    cy.getByClass('govuk-button').contains('Upload file').click();
    cy.attachFixtureFile('input[type="file"]','Finance Documents.docx','Finance Documents.docx','application/vnd.openxmlformats-officedocument.wordprocessingml.document');
    cy.getByClass('govuk-button').contains('Upload file').click();
    cy.get('button.govuk-button').contains('Save and continue').click();


     }


    static clickFinanceAndOperationsIfNotStarted() {
        if (cy.getById(this.selectors.TaskStatus).contains('Not started')) {
            this.clickFinanceAndOperations();
        }
    }

    static verifyTaskStatusIsCompleted() {

        cy.getById(this.selectors.TaskStatus).contains('Completed')  

    }  
    


}
    




export default FinanceAndOperations;




