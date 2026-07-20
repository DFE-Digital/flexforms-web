
export class SchoolImprovement{

static selectors = {

        SchoolImprovement: 'group-about-the-trust-that-academies-are-joining-task-school-improvement',
        TaskStatus: 'task-school-improvement-status',
        schoolImprovementModelChangeLink:'field-schoolimprovementmodel-change-link',
        ClickChooseFile: 'upload-file-schoolImprovementModel',
        filePath: 'C:/Users/nsadana/Downloads/School Improvement Sheet.xlsx',


    }

    static clickSchoolImprovement() 
        {
            cy.getById(this.selectors.SchoolImprovement).contains('School improvement').click();
        }

    static clickSchoolImprovementIfNotStarted() {
        if (cy.getById(this.selectors.TaskStatus).contains('Not started')) {
            this.clickSchoolImprovement();
        }
    }
    static clickChangeLinkFieldSchoolImprovement() {
        cy.getById(this.selectors.schoolImprovementModelChangeLink).click();
        this.uploadFile();

    }

    static uploadFile() {

            cy.getById(this.selectors.ClickChooseFile).click();
            cy.get('input[type="file"]').selectFile(this.selectors.filePath);
            cy.getByClass('govuk-button').contains('Upload file').click();
            cy.wait(2000);
            cy.get('button.govuk-button').contains('Save and continue').click();
            cy.wait(2000);
        }





    static verifyTaskStatusIsCompleted() {

        cy.getById(this.selectors.TaskStatus).contains('Completed')

    }  
    


}
    
export default SchoolImprovement;




