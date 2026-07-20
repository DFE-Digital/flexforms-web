
export class ReasonsAndBenefitsIncoming{
    static selectors = {
        reasonsAndBenefitsTask: 'group-about-the-trust-that-academies-are-joining-task-reason-and-benefits',
        change: 'govuk-link',
        questions: 'govuk-summary-list__key',
        reasonsAndBenefitsSaveButton: 'reasons-and-benefits-save-button',
        strategicNeedsData: 'Data_reasonAndBenefitsTrustStrategicNeeds',
        maintainAndImproveData: 'Data_reasonAndBenefitsAcademiesMaintainImprove',
        benefitsTrustData: 'Data_reasonAndBenefitsAcademiesBenefitTrust',
        saveAndContinueButton: 'save-task-summary-button',
        TaskcompleteCheckbox: 'IsTaskCompleted',
        Clickchangestrategic:'field-reasonandbenefitsacademiesstrategicneeds-change-link',
        ClickchangeMaintain:'field-reasonandbenefitsacademiesmaintainimprove-change-link',
        ClickchangeBenefits:'trust',
        Taskstatus:'task-reason-and-benefits-status',
        changeLinkStrategicneeds:'field-reasonandbenefitstruststrategicneeds-change-link',
        changeLinkBenefitMaintain:'field-reasonandbenefitstrustdevelopmentalneeds-change-link',
        changeLinkWorkedTogether:'field-reasonandbenefitstrustacademiestrustsworkedtogether-change-link',
        TextareaStrategicNeeds:'Data_reasonAndBenefitsTrustDevelopmentalNeeds',
        TextareaMaintainAndBenefits:'Data_reasonAndBenefitsTrustDevelopmentalNeeds',
        TextareaWorkedTogether:'Data_reasonAndBenefitsTrustHowHaveAcademiesTrustsWorkedTogether',
        RadioWorkTogetherinPastYes:'Data_reasonAndBenefitsTrustAcademiesTrustsWorkedTogether_',
        RadioWorkTogetherinPastNo:'Data_reasonAndBenefitsTrustAcademiesTrustsWorkedTogether_-2',
    }



        static clickReasonsAndBenefits() {
        
        cy.getById( this.selectors.reasonsAndBenefitsTask).click();

   }

        static clickReasonsIfNotStarted() {
            if (cy.getById(this.selectors.Taskstatus).contains('Not started')) {
                this.clickReasonsAndBenefits();
            }
        }

       
static EnterStrategicNeeds(strategicNeedsText: string) {

    //Change Strategic Needs
     cy.getById(this.selectors.changeLinkStrategicneeds).contains('Change').click();
     cy.getById(this.selectors.strategicNeedsData).type(strategicNeedsText);
     cy.SaveAndContinue();
}
// Change Maintain and Benefits
    static EnterMaintainAndBenefits(maintainAndImproveText: string) {
    cy.getById(this.selectors.changeLinkBenefitMaintain).contains('Change').click();
    cy.getById(this.selectors.TextareaMaintainAndBenefits).type(maintainAndImproveText);
    cy.SaveAndContinue();

    }

// Change Worked Together
    static EnterWorkedTogether(workedTogetherText: string, workTogetherOption: string) {
    cy.getById(this.selectors.changeLinkWorkedTogether).contains('Change').click();
    {
     if (workTogetherOption == 'Yes') {
        
         cy.getById(this.selectors.RadioWorkTogetherinPastYes).click();
         cy.SaveAndContinue();
         cy.getById(this.selectors.TextareaWorkedTogether).type(workedTogetherText);
     }

       else  if (workTogetherOption == 'No') {
      cy.getById(this.selectors.RadioWorkTogetherinPastNo).should('not.be.checked');
      cy.getById(this.selectors.RadioWorkTogetherinPastNo).check();
    }
        cy.SaveAndContinue();
        this.clickCompleteCheckbox();
        cy.getById('save-task-summary-button').click();
}
        

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


}
export default ReasonsAndBenefitsIncoming;