
export class ReasonsAndBenefits{
    static selectors = {
        reasonsAndBenefitsTask: 'group-about-transferring-academies-task-reason-and-benefits',
        change: 'govuk-link',
        questions: 'govuk-summary-list__key',
        reasonsAndBenefitsSaveButton: 'reasons-and-benefits-save-button',
        strategicNeedsData: 'Data_reasonAndBenefitsAcademiesStrategicNeeds',
        benefitsTrustData: 'Data_reasonAndBenefitsAcademiesMaintainImprove',
        saveAndContinueButton: 'save-task-summary-button',
        TaskcompleteCheckbox: 'IsTaskCompleted',
        Clickchangestrategic:'field-reasonandbenefitsacademiesstrategicneeds-change-link',
        ClickchangeMaintain:'field-reasonandbenefitsacademiesmaintainimprove-change-link',
        ClickchangeBenefits:'trust',
        Taskstatus:'task-reason-and-benefits-status',
        StrategicNeedsChangeLink:'field-reasonandbenefitsacademiesstrategicneeds-change-link',
        BenefitsChangeLink:'field-reasonandbenefitsacademiesmaintainimprove-change-link'
    }
    static selectReasonsAndBenefits() {
        
        cy.getById( this.selectors.reasonsAndBenefitsTask).first().click();

}

        static clickReasonsIfNotStarted() {
            if (cy.getByClass('govuk-tag govuk-tag--grey').contains('Not started')) {
                this.selectReasonsAndBenefits();
            }
        }
        


   static ClickChangeforStrategicNeeds(StrategicNeedsText: string){
  
    cy.getById(this.selectors.StrategicNeedsChangeLink).contains('Change').click();
    cy.getById(this.selectors.strategicNeedsData).type(StrategicNeedsText);
    cy.SaveAndContinue();
                
   }


   static ClickChangeforBenefits(BenefitsText: string) { 
        //Enter the benefits text
        cy.getById(this.selectors.BenefitsChangeLink).contains('Change').click();
        cy.getById(this.selectors.benefitsTrustData).type(BenefitsText);
        //Click the save and continue button
        cy.SaveAndContinue();
    }
   
   static clickCompleteCheckbox (){

    // Check if the task is not completed
        if (cy.getById(this.selectors.TaskcompleteCheckbox).should('not.be.checked')) {
            cy.getById(this.selectors.TaskcompleteCheckbox).check();
        }
   }

static verifyTaskStatusIsCompleted() {
    cy.getById(this.selectors.Taskstatus).contains('Completed')
}

   // cy.getById("field...improve").contains("Change").click()
  //  cy.getByClass('govuk-summary-list').each(($row, index) => {
  // Find the link within the current row
 // cy.wrap($row).find('a').contains('Change').click();

  // Perform any assertions or actions after clicking
  //cy.url().should('include', `/expected-path-${index}`);

  // Navigate back to the table if needed
 // cy.getById('save-and-continue-button').click();});

}

export default ReasonsAndBenefits;
