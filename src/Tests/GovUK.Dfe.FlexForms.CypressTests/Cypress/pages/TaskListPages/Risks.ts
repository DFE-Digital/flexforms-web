
export class Risks{

static selectors = {

        Risks: 'group-about-transferring-academies-task-risks',
        TaskStatus: 'task-risks-status',
        taskCompleted:'task-details-of-trusts-status',
        saveandcontinue:'save-task-summary-button',
        Markcompletecheckbox:'IsTaskCompleted',
        DueDilgeneChangeLink:'field-risksduediligence-change-link',
        DueDilgeneTextArea:'Data_risksDueDiligence',
        RisksPupilNumberChangeLink:'field-riskspupilnumbers-change-link',
        RisksPupilNumberYesRadio:'Data_risksPupilNumbers_',
        RisksPupilNumberNoRadio:'Data_risksPupilNumbers_-2',
        ClickChooseFile:'upload-file-risksUploadPupilNumbers',
        filepath:'C:/Users/nsadana/Downloads/Risks Pupil Numbers.xlsx',
        TypeOfTransferChangeLink:'field-riskstransfertype-change-link',
        OtherRisksChangeLink:'field-risksotherrisks-change-link',
        OtherRisksYesRadio:'Data_risksOtherRisks_',
        OtherRisksNoRadio:'Data_risksOtherRisks_-2',
        TypeOfTransfer1:'Data_risksTransferType_',
        TypeOfTransfer2:'Data_risksTransferType_-2',
        TypeOfTransfer3:'Data_risksTransferType_-3',
        TrasferDeficitYes:'Data_risksFinancialDeficit_',
        TrasferDeficitNo:'Data_risksFinancialDeficit_-2',
        RiskSummaryTextArea:'Data_risksRiskManagement',
        ClickChooseFileForecast:'upload-file-risksFinancialForecast',
        filepathForecast:'C:/Users/nsadana/Downloads/Financial Forecast.xlsx',
        FinancesPooledChangeLink:'field-risksfinancespooled-change-link',
        GAGPooledYesRadio:'Data_risksFinancesPooled_',
        ReservePooledRadio:'Data_risksFinancesPooled_-2',
        NotPooledRadio:'Data_risksFinancesPooled_-3',
        SurplusFundsChangeLink:'field-risksreservestransfer-change-link',
        SurplusFundsTextarea:'Data_risksReservesTransfer'



    }

    static clickRisks() 
        {
            cy.getById(this.selectors.Risks).contains('Risks').click();
        }

    static clickRisksIfNotStarted() {
        if (cy.getById(this.selectors.TaskStatus).contains('Not started')) {
            this.clickRisks();
        }
    }

    static ClickChangeDueDiligence(DueDiligenceText: string) {
        cy.getById(this.selectors.DueDilgeneChangeLink).contains('Change').click();
        cy.getById(this.selectors.DueDilgeneTextArea).type(DueDiligenceText);
        cy.SaveAndContinue();
    }
    static ClickChangeRisksPupilNumber(RisksPupilNumberText: string) {
        cy.getById(this.selectors.RisksPupilNumberChangeLink).contains('Change').click();
        if (RisksPupilNumberText == 'Yes'){
            cy.getById(this.selectors.RisksPupilNumberYesRadio).check();
            cy.SaveAndContinue();
            // Upload file
            this.uploadFile();
    }

        else if (RisksPupilNumberText === 'No'){
            cy.getById(this.selectors.RisksPupilNumberNoRadio).check();
            cy.SaveAndContinue();

        }

    }

     static uploadFile() {

            cy.getById(this.selectors.ClickChooseFile).click();
            cy.get('input[type="file"]').selectFile(this.selectors.filepath);
            cy.getByClass('govuk-button').contains('Upload file').click();
            cy.get('button.govuk-button').contains('Save and continue').click();
        }

        static UploadFinacialForecast() {
             cy.getById(this.selectors.ClickChooseFileForecast).click();
            cy.get('input[type="file"]').selectFile(this.selectors.filepathForecast);
            cy.getByClass('govuk-button').contains('Upload file').click();
            cy.get('button.govuk-button').contains('Save and continue').click();
        }
        



    static ClickChangeTypeOfTransfer() {
        cy.getById(this.selectors.TypeOfTransferChangeLink).contains('Change').click();
        cy.getById(this.selectors.TypeOfTransfer1).check();
        cy.SaveAndContinue();
        cy.getById(this.selectors.TrasferDeficitYes).check();
        cy.SaveAndContinue();
        this.UploadFinacialForecast();
      //  cy.getById(this.selectors.OtherRisksNoRadio).check();
        //cy.SaveAndContinue();

    }
    static ClickChangeOtherRisks(OtherRisksText: string) {
      //  cy.getById(this.selectors.OtherRisksChangeLink).contains('Change').click();
        cy.getById(this.selectors.OtherRisksYesRadio).check();
        cy.SaveAndContinue();
        cy.getById(this.selectors.RiskSummaryTextArea).type(OtherRisksText);
        cy.SaveAndContinue();
    }

    static ClickChangeFinancesPooled(FinancesPooledOption: string) {
        cy.getById(this.selectors.FinancesPooledChangeLink).contains('Change').click();
        if (FinancesPooledOption == 'GAGPooled' || FinancesPooledOption == 'Reserve Pooled' || FinancesPooledOption == 'Not Pooled'){
            cy.getById(this.selectors.GAGPooledYesRadio).check();
            cy.SaveAndContinue();
            cy.getById(this.selectors.OtherRisksYesRadio).should('be.checked');
            cy.wait(5000);
            cy.SaveAndContinue();
            cy.getById(this.selectors.RiskSummaryTextArea).should('exist');
            cy.SaveAndContinue();

        }
    }

    static ClickChangeSurplusFunds(SurplusFundsOption: string) {

        cy.getById(this.selectors.SurplusFundsChangeLink).contains('Change').click();
        cy.getById(this.selectors.SurplusFundsTextarea).type(SurplusFundsOption);
        cy.SaveAndContinue();
    }


    static verifyTaskStatusIsCompleted() {

        cy.getById(this.selectors.TaskStatus).contains('Completed')

    }  
    
    static clickMarkCompleteCheckbox() {
        cy.getById(this.selectors.Markcompletecheckbox).check();
        cy.getById(this.selectors.Markcompletecheckbox).should('be.checked');
    }
    
    static clickSaveAndContinue() {
        cy.getById(this.selectors.saveandcontinue).contains('Save and continue').click();
        return this;
    }



}
    




export default Risks;




