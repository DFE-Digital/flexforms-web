
export class Members{

static selectors = {

        Members: 'group-about-the-trust-that-academies-are-joining-task-members',
        TaskStatus: 'task-members-status',
        AddAMember:'membersAfterTransfer-add-item',
        MemberName:'Data_memberName',
        ExistingMemberRadio:'Data_existingMember_',
        NewMemberRadio:'Data_existingMember_-2',
        MemberRoleTrustee:'Data_additionalRoles_',
        MemberRoleLocalGoverningBody:'Data_additionalRoles_-2',
        MemberRoleNone:'Data_additionalRoles_-3',
        MemberRoleTextarea:'Data_pastRoles',
        AddLeavingMember:'membersLeaving-add-item',
        LeavingMemberName:'Data_memberLeavingName',



    }

    static clickMembers() 
        {
            cy.getById(this.selectors.Members).contains('Members').click();
        }

    static clickMembersIfNotStarted() {
        if (cy.getById(this.selectors.TaskStatus).contains('Not started')) {
            this.clickMembers();
        }
    }

    static addAnExistingMember( Member1: string){
        cy.getById(this.selectors.AddAMember).click();
        cy.getById(this.selectors.MemberName).eq(0).type(Member1);
        cy.SaveAndContinue();
        cy.getById(this.selectors.ExistingMemberRadio).eq(0).check();
        cy.SaveAndContinue();
        cy.getById(this.selectors.MemberRoleTrustee).check();
        cy.SaveAndContinue();
      
    } 
   static addANewMember( Member2: string, PastRoles: string){
        cy.getById(this.selectors.AddAMember).click();
        cy.getById(this.selectors.MemberName).eq(0).type(Member2);
        cy.SaveAndContinue();
        cy.getById(this.selectors.NewMemberRadio).eq(0).check();
        cy.SaveAndContinue();
        cy.getById(this.selectors.MemberRoleTextarea).eq(0).type(PastRoles);
        cy.SaveAndContinue();
        cy.getById(this.selectors.MemberRoleLocalGoverningBody).check();
        cy.SaveAndContinue();

   } 

     static VerifyAddedMember( Member1: string){
          cy.getByClass('govuk-notification-banner__content').should('contain.text', Member1 + ' has been added to Members after the transfer');
    }


   static addALeavingMember( LeavingMember: string)
    {
        cy.getById(this.selectors.AddLeavingMember).click();
        cy.getById(this.selectors.LeavingMemberName).eq(0).type(LeavingMember);
        cy.SaveAndContinue();
    }

    static LeavingMemberVerification( LeavingMember: string){
        cy.getByClass('govuk-notification-banner__heading').should('contain.text', LeavingMember + ' has been added to Existing members who will be leaving');
    }


    static verifyTaskStatusIsCompleted() {

        cy.getById(this.selectors.TaskStatus).contains('Completed')

    }  
    


}
    




export default Members;




