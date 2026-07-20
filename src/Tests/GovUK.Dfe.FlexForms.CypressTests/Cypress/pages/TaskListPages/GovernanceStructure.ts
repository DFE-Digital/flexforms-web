
export class GovernanceStructure{

static selectors = {

        GovernanceStructure: 'group-about-the-trust-that-academies-are-joining-task-governance-structure',
        TaskStatus: 'task-governance-structure-status',
        GovernanceStructureChangeLink:'field-governanceteamconfirmation-change-link',
        ProposedGovernanceStructureChangeLink:'field-governancestructureafterthetransferpploaddocuments-change-link',
        GovernanceRolesYesRadio:'Data_governanceTeamConfirmation_',
        GovernanceRolesNoRadio:'Data_governanceTeamConfirmation_-2',
        TrustPlanTextArea:'Data_governanceTeamExplanation',
        ClickChooseFile:'upload-file-governanceStructureAfterTheTransferPploadDocuments',
        filePath: 'C:/Users/nsadana/Downloads/Governance Structure.pdf',




    }

    static clickGovernanceStructure() 
        {
            cy.getById(this.selectors.GovernanceStructure).contains('Governance Structure').click();
        }

    static clickGovernanceStructureIfNotStarted() {
        if (cy.getById(this.selectors.TaskStatus).contains('Not started')) {
            this.clickGovernanceStructure();
        }
    }

     static uploadFile() {

            cy.getById(this.selectors.ClickChooseFile).click();
            cy.get('input[type="file"]').selectFile(this.selectors.filePath);
            cy.getByClass('govuk-button').contains('Upload file').click();
            cy.get('button.govuk-button').contains('Save and continue').click();
        }
    static ClickChangeGovernanceStructure( governanceRolesOption: string, trustPlanText: string) {
        cy.getById(this.selectors.GovernanceStructureChangeLink).click();
        if (governanceRolesOption == 'Yes') {
            cy.getById(this.selectors.GovernanceRolesYesRadio).click();
            
        } 
        else  if (governanceRolesOption == 'No') {
            cy.getById(this.selectors.GovernanceRolesNoRadio).click();
            cy.SaveAndContinue();
            cy.getById(this.selectors.TrustPlanTextArea).type(trustPlanText);       
        }
        cy.SaveAndContinue();
    }

    static ClickChangeProposedGovernanceStructure() {
        cy.getById(this.selectors.ProposedGovernanceStructureChangeLink).click();
        this.uploadFile();

    }






    static verifyTaskStatusIsCompleted() {

        cy.getById(this.selectors.TaskStatus).contains('Completed')

    }  
    


}
    




export default GovernanceStructure;




