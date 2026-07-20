
export class HighQualityInclusiveEducation{

static selectors = {

        Leadership: 'group-about-the-trust-that-academies-are-joining-task-leadership-and-work-force',
        TaskStatus: 'task-leadership-and-work-force-status',
        LeadershipChangeLink:'field-leadershipandworkforcewilltheleadershipcentralteamchange-change-link',
        LeadershipChangeRadioYes:'Data_leadershipAndWorkForceWillTheLeadershipCentralTeamChange_',
        LeadershipChangeRadioNo:'Data_leadershipAndWorkForceWillTheLeadershipCentralTeamChange_-2',
        LeadershipChnageTextArea:'Data_leadershipAndWorkForceHowWillTheLeadershipCentralTeamChange',

    }

    static clickLeadership() 
        {
            cy.getById(this.selectors.Leadership).contains('Leadership and work force').click();
        }

    static clickLeadershipIfNotStarted() {
        if (cy.getById(this.selectors.TaskStatus).contains('Not started')) {
            this.clickLeadership();
        }
    }

    static ClickChangeLeadershipAndWorkForce( leadershipAndWorkForceText: string, leadershipAndWorkForceDescriptionText: string) {
        cy.getById(this.selectors.LeadershipChangeLink).click();
        if (leadershipAndWorkForceText === 'Yes'){
            cy.getById(this.selectors.LeadershipChangeRadioYes).check();
            cy.SaveAndContinue();
            cy.getById(this.selectors.LeadershipChnageTextArea).type(leadershipAndWorkForceDescriptionText);
 }
        else if (leadershipAndWorkForceText === 'No'){
            cy.getById(this.selectors.LeadershipChangeRadioNo).check();
        }
        cy.SaveAndContinue();

    }








    static verifyTaskStatusIsCompleted() {

        cy.getById(this.selectors.TaskStatus).contains('Completed')

    }  
    


}
    




export default HighQualityInclusiveEducation;




