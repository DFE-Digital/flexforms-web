import {EnvUsername} from "../Constants/cypressConstants";

export class Contributors{

    static selectors = {
        addContributor: 'add-a-contributor',
        proceedBtn: 'proceed-to-application-form',
        contributor1:'contributor-1',
        contributor2:'contributor-2',
        contributoremail: 'EmailAddress',
        contributorname:'Name',
        sendinvite:'send-email-invite',
        removenewcontributor:'remove-contributor-2',
        emailText: 'test@test.com',
        cancel: 'cancel',
        inviteContributor:'invite-contributors' ,
        newcontributoremail: 'ContributorTestuser@gov.uk',
        newcontributorname: 'Test User'
    }

    
static addContributor() {
    cy.getById(this.selectors.addContributor).contains('Add a contributor').click();
    cy.getById(this.selectors.contributoremail).type(this.selectors.newcontributoremail);
    cy.getById(this.selectors.contributorname).type(this.selectors.newcontributorname);
    cy.getById(this.selectors.cancel).contains('Cancel');
    cy.getById(this.selectors.sendinvite).click();
    return this;
}
static verifyContributor(){
    // Verify if the contributor is added by checking the username
    cy.getById(this.selectors.contributor1).contains(Cypress.expose(EnvUsername));
}

static verifyNewContributor() {

    // Verify if the new contributor is added by checking the email
    cy.getById(this.selectors.contributor2).contains(this.selectors.newcontributoremail);
    // Verify if the new contributor is added by checking the username
    cy.getById(this.selectors.contributor2).contains(this.selectors.newcontributorname);
    //Verify if there is option to Remove Contributor
    cy.getById(this.selectors.removenewcontributor).contains('Remove');
}

static ClickProceedBtn() {
    cy.getById(this.selectors.proceedBtn).contains('Go to application form').click();
    return this;
}
// This method is used to invite contributors from Task List Page
static inviteContributors() {
    cy.getById(this.selectors.inviteContributor).contains('Invite contributors').click();
}

}
export default Contributors;
