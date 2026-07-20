
export class Trustees{

static selectors = {

        Trustees: 'group-about-the-trust-that-academies-are-joining-task-trustees',
        TaskStatus: 'task-trustees-status',
        AddATrustee:'trusteesAfterTransfer-add-item',
        TrusteeName:'Data_trusteeName',
        ExistingTrusteeRadio:'Data_existingTrustee_',
        NewTrusteeRadio:'Data_existingTrustee_-2',
        TrusteeFutureRoleTextArea:'Data_trusteeFutureRoles',
        TrusteePastRoleTextArea:'Data_trusteePastRoles',
        LocalGoverningBodyRoleYes:'Data_trusteeLocalGoverningBody_',
        LocalGoverningBodyRoleNo:'Data_trusteeLocalGoverningBody_-2',
        AddAPersonLeavingTrustee:'trusteesLeaving-add-item',
        LeavingTrusteeName:'Data_trusteeLeavingName',



    }

    static clickTrustees() 
        {
            cy.getById(this.selectors.Trustees).contains('Trustees').click();
        }

    static clickTrusteesIfNotStarted() {
        if (cy.getById(this.selectors.TaskStatus).contains('Not started')) {
            this.clickTrustees();
        }
    }

    static addAnExistingTrustee(Trusttee1: string, FutureRoles: string, LocalGoverningBodyRole: string){

        cy.getById(this.selectors.AddATrustee).click();
        cy.SaveAndContinue();
        cy.getById(this.selectors.TrusteeName).eq(0).type(Trusttee1);
        cy.SaveAndContinue();
        cy.getById(this.selectors.ExistingTrusteeRadio).eq(0).check();
        cy.SaveAndContinue();
        cy.getById(this.selectors.TrusteeFutureRoleTextArea).type(FutureRoles);
        cy.SaveAndContinue();
        if  (LocalGoverningBodyRole =='Yes'){
            cy.getById(this.selectors.LocalGoverningBodyRoleYes).check();
        }
        else {
            cy.getById(this.selectors.LocalGoverningBodyRoleNo).check();
        }
        cy.SaveAndContinue();
    }

    static verifyAddedTrustee( Trusttee1: string){
            cy.getByClass('govuk-notification-banner__heading').should('contain.text', Trusttee1 + ' has been added to Trustees after the transfer');
    }


    static addANewTrustee( Trusttee2: string, PastRoles: string, LocalGoverningBodyRole: string, FutureRoles: string){
        cy.getById(this.selectors.AddATrustee).click();
        cy.SaveAndContinue();
        cy.getById(this.selectors.TrusteeName).eq(0).type(Trusttee2);
        cy.SaveAndContinue();
        cy.getById(this.selectors.NewTrusteeRadio).eq(0).check();
        cy.SaveAndContinue();
        cy.getById(this.selectors.TrusteePastRoleTextArea).type(PastRoles);
        cy.SaveAndContinue();
        cy.getById(this.selectors.TrusteeFutureRoleTextArea).type(FutureRoles);
        cy.SaveAndContinue();
        if  (LocalGoverningBodyRole =='Yes'){
            cy.getById(this.selectors.LocalGoverningBodyRoleYes).check();

        }
        else {
            cy.getById(this.selectors.LocalGoverningBodyRoleNo).check();
        }
        cy.SaveAndContinue();
    }

    static addALeavingTrustee( LeavingTrustee: string)
    {
        cy.getById(this.selectors.AddAPersonLeavingTrustee).click();
        cy.getById(this.selectors.LeavingTrusteeName).eq(0).type(LeavingTrustee);
        cy.SaveAndContinue();

    }

    static LeavingTrusteeVerification( LeavingTrustee: string){
        cy.getByClass('govuk-notification-banner__heading').should('contain.text', LeavingTrustee + ' has been added to Existing trustees who will be leaving');
    }







    static verifyTaskStatusIsCompleted() {

        cy.getById(this.selectors.TaskStatus).contains('Completed')

    }  
    


}
    




export default Trustees;




