
export class Declaration{

static selectors = {

        Declaration: 'group-declaration-task-declaration-from-all-chairs-of-trustees',
        TaskStatus: 'task-declaration-from-all-chairs-of-trustees-status',
        JoiningAcademy:'govuk-summary-list__key',
        LeavingAcademy:'govuk-summary-list__key',
        JoiningAcademyViewLink:'a[href*="trust-declarations-joining"]',
        LeavingAcademyViewLink:'a[href*="trust-declarations-leaving"]',
        EqualityDecisionJoiningradio1:'Data_equalities-duties-decision_',
        EqualityDecisionLeavingRadio2:'Data_equalities-duties-decision-leaving_-2',
        JoiningChairTrusteeName:'Data_chairName-joining',
        LeavingChairTrusteeName:'Data_chairName-leaving',
        EnterSigningDate:'Data_dateSigned-joining.Day',
        EnterSigningMonth:'Data_dateSigned-joining.Month',
        EnterSigningYear:'Data_dateSigned-joining.Year',
        EnterLeavingDate:'Data_dateSigned-leaving.Day',
        EnterLeavingMonth:'Data_dateSigned-leaving.Month',
        EnterLeavingYear:'Data_dateSigned-leaving.Year',



    }

    static clickDeclaration() 
        {
            cy.getById(this.selectors.Declaration).contains('Declaration from all chairs of trustees').click();
        }

    static clickDeclarationIfNotStarted() {
        if (cy.getById(this.selectors.TaskStatus).contains('Not started')) {
            this.clickDeclaration();
        }
    }
     static ClickViewJoiningAcademyDeclaration(JoiningAcademy :string,JoiningChairName: string,Date: string, Month: string, Year: string){

       if (cy.getByClass(this.selectors.JoiningAcademy).contains(JoiningAcademy)){

        cy.get(this.selectors.JoiningAcademyViewLink).click();
        cy.getById(this.selectors.EqualityDecisionJoiningradio1).check();
        cy.getById(this.selectors.JoiningChairTrusteeName).type(JoiningChairName);
        this.EnterSigningDateofJoining(Date,Month,Year);
        cy.SaveAndContinue();

       }   

     }
        static EnterSigningDateofJoining(Date: string, Month: string, Year: string){
        cy.getById(this.selectors.EnterSigningDate).type(Date);
        cy.getById(this.selectors.EnterSigningMonth).type(Month);
        cy.getById(this.selectors.EnterSigningYear).type(Year);
       
    }
    static EnterSigningDateofLeaving(Date: string, Month: string, Year: string){
        cy.getById(this.selectors.EnterLeavingDate).type(Date);
        cy.getById(this.selectors.EnterLeavingMonth).type(Month);
        cy.getById(this.selectors.EnterLeavingYear).type(Year);
        
    }


     static ClickViewLeavingAcademyDeclaration(LeavingAcademy:string,LeavingChairTrusteeName: string,Date: string, Month: string, Year: string){
        if (cy.getByClass(this.selectors.LeavingAcademy).contains(LeavingAcademy)){

        cy.get(this.selectors.LeavingAcademyViewLink).click();
        cy.getById(this.selectors.EqualityDecisionLeavingRadio2).check();
        cy.getById(this.selectors.LeavingChairTrusteeName).type(LeavingChairTrusteeName);
        this.EnterSigningDateofLeaving(Date,Month,Year);
        cy.SaveAndContinue();
       }

     }

    static verifyTaskStatusIsCompleted() {

        cy.getById(this.selectors.TaskStatus).contains('Completed')

    }  
    


}
    




export default Declaration;




