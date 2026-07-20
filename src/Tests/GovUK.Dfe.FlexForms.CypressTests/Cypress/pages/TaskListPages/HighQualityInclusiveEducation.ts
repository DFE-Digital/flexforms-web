
export class HighQualityInclusiveEducation{

static selectors = {

        HighQualityInclusiveEducation: 'group-about-the-trust-that-academies-are-joining-task-high-quality-and-inclusive-education',
        TaskStatus: 'task-high-quality-and-inclusive-education-status',
        ClickchangeHighQualityInclusiveEducationQuality:'field-highqualityandinclusiveeducationquality-change-link',
        ClickchangeHighQualityInclusiveEducationImpactchange:'field-highqualityandinclusiveeducationimpact-change-link',
        QualityTextArea: 'Data_highQualityAndInclusiveEducationQuality',
        ImpactTextArea: 'Data_highQualityAndInclusiveEducationImpact',
        TaskcompleteCheckbox: 'IsTaskCompleted',

    }

    static clickHighQualityInclusiveEducation() 
        {
            cy.getById(this.selectors.HighQualityInclusiveEducation).contains('High-quality and inclusive education').click();
        }

    static clickHighQualityInclusiveEducationIfNotStarted() {
        if (cy.getById(this.selectors.TaskStatus).contains('Not started')) {
            this.clickHighQualityInclusiveEducation();
        }
    }
    static ClickChangeHighQualityInclusiveEducationQuality( highQualityInclusiveEducationQualityText: string) {
        cy.getById(this.selectors.ClickchangeHighQualityInclusiveEducationQuality).click();
        cy.getById(this.selectors.QualityTextArea).type(highQualityInclusiveEducationQualityText);
        cy.SaveAndContinue();
    }
    static ClickChangeHighQualityImpactChange( highQualityInclusiveEducationImpactText: string) {
        cy.getById(this.selectors.ClickchangeHighQualityInclusiveEducationImpactchange).click();
        cy.getById(this.selectors.ImpactTextArea).type(highQualityInclusiveEducationImpactText);
        cy.SaveAndContinue();
        this.clickCompleteCheckbox();
        cy.getById('save-task-summary-button').click();
        this.verifyTaskStatusIsCompleted();

    }

     static clickCompleteCheckbox (){

    // Check if the task is not completed
        if (cy.getById(this.selectors.TaskcompleteCheckbox).should('not.be.checked')) {
            cy.getById(this.selectors.TaskcompleteCheckbox).check();
        }
   }

    




    static verifyTaskStatusIsCompleted() {

        cy.getById(this.selectors.TaskStatus).contains('Completed')

    }  
    


}
    

export default HighQualityInclusiveEducation;




