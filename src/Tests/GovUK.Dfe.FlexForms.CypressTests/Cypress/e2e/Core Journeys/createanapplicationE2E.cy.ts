// Import the necessary classes and methods
import GovUKPage from '../../pages/GovUKPage';
import TestLoginPage from '../../pages/TestLoginPage';
import Dashboardpage from '../../pages/Dashboardpage';
import { Logger } from '../../Common/logger';
import '../../support/commands';
import Contributors from '../../pages/ContributorsPage';
import ReasonsAndBenefits from '../../pages/TaskListPages/ReasonsAndBenefitsPageOutgoing'; 
import Academies from '../../pages/TaskListPages/DetailsofAcademies';
import OutgoingTrust from '../../pages/TaskListPages/OutgoingTrust';
import Risks from '../../pages/TaskListPages/Risks';
import IncomingTrust from '../../pages/TaskListPages/IncomingTrustDetails';
import ReasonsAndBenefitsIncoming from '../../pages/TaskListPages/ReasonsAndBenefitsIncoming';
import HighQualityInclusiveEducation from '../../pages/TaskListPages/HighQualityInclusiveEducation';
import SchoolImprovement from '../../pages/TaskListPages/SchoolImprovement';
import FinanceAndOperations from '../../pages/TaskListPages/FinanceAndOperations';
import Leadership from '../../pages/TaskListPages/Leadership';
import Members from '../../pages/TaskListPages/Members';
import Trustees from '../../pages/TaskListPages/Trustees';
import GovernanceStructure from '../../pages/TaskListPages/GovernanceStructure';
import Declaration from '../../pages/TaskListPages/Declaration';
import 'cypress-file-upload';


describe('Create an Application', () => {

 beforeEach(() => {
        cy.login();      
        cy.executeAccessibilityTests();
    });

  it('Should Navigate to Dashboard Page', () => {
    Logger.log("Dashboard Loaded"); 

   // Logger.log("Clicking the start button on the dashboard");
     
    Logger.log("Clicking the Start New Application button");
    Dashboardpage.clickStartBtn();
    
    Logger.log("Verify if redirected to Dashboard page");
    cy.url().should('include', '/contributors'); 

    // Add a contributor 
    Logger.log("Adding a contributor");
    Contributors.addContributor();
    // Verify if the navigated to Contributors Page
    Logger.log("Verify if redirected to Contributors page");
    cy.url().should('include', '/contributors'); 

    // Verify if the contributor is added
    Logger.log("Verifying if the contributor is added");
    Contributors.verifyNewContributor();
    Logger.log("Clicking the Proceed button on Contributors page");
    Contributors.ClickProceedBtn();

    // Verify if the navigated to Task List Page
   Logger.log("Verify if redirected to Task List page");
     cy.url().should('include', '/applications/');
   // Click on the Invite Contributors button from Task List page
      Contributors.inviteContributors();
   // Logger.log("Clicking the Proceed button on Contributors page");
       Contributors.ClickProceedBtn();
    // Verify if the navigated to Task List Page
     cy.url().should('include', '/applications/');
      Dashboardpage.extractReferenceNumber();

    //**About the Trust that academies are joining**/

    //1. Select Incoming Trust task
    Logger.log("Clicking the Incoming Trust task");
    IncomingTrust.clickDetailsofTrustIfNotStarted();
    // Verify if the navigated to Incoming Trust page
    cy.url().should('include', '/incoming-trust-details');
     // Click on the Mark Complete checkbox
    Logger.log("Clicking the Mark Complete checkbox");
    IncomingTrust.clickMarkCompleteCheckbox();
    // Click on the Save and Continue button
    Logger.log("Clicking the Save and Continue button on Details of Academies page");
    IncomingTrust.clickSaveAndContinue();
    // Verify if the Task status is Completed
    IncomingTrust.verifyTaskStatusIsNotCompleted();
    //Click Add a Trust button
    IncomingTrust.AddTrust();
    // Find a Trust
    IncomingTrust.findTrust('CANONS HIGH SCHOOL');
    // Click on Search button
    IncomingTrust.ClicksearchTrust();
    // Verify the Trust is searched correctly
   IncomingTrust.VerifyTrust('CANONS HIGH SCHOOL');
  // Confirm the selection
    IncomingTrust.confirmselection();
    //Click Continue
    IncomingTrust.continueButton();
    //Select Type of Trust
    IncomingTrust.SelectTypeOfTrust();
    // Enter Accounting Officer Details
    IncomingTrust.EnterAccountingOfficerDetails('Test Officer','0123456789', 'officer@gov.uk');
    // Enter Head of Finance Details
    IncomingTrust.EnterHeadofFinanceDetails('Finance Officer','0987654321', 'finance@gov.uk');
    //Enter Chair of Trustees Details
    IncomingTrust.EnterChairofTrusteesDetails('Chair Trustee','0987654321', 'chair@gov.uk')
    //Enter Details of Main Contact
    IncomingTrust.EnterDetailsofMainContact('Main Contact','Director','0123456789', 'main@gov.uk');
    // Upload Board Resolution File
    IncomingTrust.uploadFile();
    //Verify The trust that academies are joining has been added
    IncomingTrust.verifyFileUploadSuccessMessage();
    // Click on the Mark Complete checkbox
    cy.clickMarkCompleteCheckbox();
    cy.SaveTaskSummary();
    // Verify if the Task status is Completed
    IncomingTrust.verifyTaskStatusIsCompleted();

  //2. Reasons and Benefits task in Incoming Trust
    ReasonsAndBenefitsIncoming.clickReasonsIfNotStarted();
    // Verify if navigated to Reasons and Benefits Incoming Trust Details page
    cy.url().should('include', 'reason-and-benefits-trust');
    // Fill in the Reasons and Benefits Incoming Trust Details form
     ReasonsAndBenefitsIncoming.EnterStrategicNeeds('Strategic needs Testing text');
      // Maintain and Benefits
       ReasonsAndBenefitsIncoming.EnterMaintainAndBenefits('Maintain and improve Testing text');
      // Worked Together

      ReasonsAndBenefitsIncoming.EnterWorkedTogether('Worked together Testing text', 'Yes');
    // Verify if the Task status is Completed
    ReasonsAndBenefitsIncoming.verifyTaskStatusIsCompleted();

 //   3.High Quality Inclusive Education
   HighQualityInclusiveEducation.clickHighQualityInclusiveEducationIfNotStarted();
  //  Verify if the navigated to High Quality Inclusive Education page
   cy.url().should('include', '/high-quality-and-inclusive-education');
   HighQualityInclusiveEducation.ClickChangeHighQualityInclusiveEducationQuality('High Quality and Inclusive Education Quality testing text');
   HighQualityInclusiveEducation.ClickChangeHighQualityImpactChange('High Quality and Inclusive Education Impact testing text');

  //  4.School Improvement
   SchoolImprovement.clickSchoolImprovementIfNotStarted();
 //   Verify if the navigated to School Improvement page
   cy.url().should('include', '/school-improvement');
   SchoolImprovement.clickChangeLinkFieldSchoolImprovement();
  //   Click on the Mark Complete checkbox
    cy.clickMarkCompleteCheckbox();
    cy.SaveTaskSummary();
      SchoolImprovement.verifyTaskStatusIsCompleted();
  //  5.Finance and Operations
   FinanceAndOperations.clickFinanceAndOperationsIfNotStarted();
  //  Verify if the navigated to Finance and Operations page
    cy.url().should('include', '/finance-and-operations');
    FinanceAndOperations.clickChangeFinanceAndOperationsGrowthPlanOption('Yes');
    FinanceAndOperations.ClickChangeChargeOnAcademies('Yes', 'Charge on Academies testing text');
    FinanceAndOperations.ClickChangeAlternativeAcademies('Yes', 'Yes', 'Alternative Agreements testing text');
    cy.clickMarkCompleteCheckbox();
    cy.SaveTaskSummary();
    FinanceAndOperations.verifyTaskStatusIsCompleted();
 //   6.Leadership
    Leadership.clickLeadershipIfNotStarted();
  //  Verify if the navigated to Leadership page
   cy.url().should('include', '/leadership-and-work-force');
    Leadership.ClickChangeLeadershipAndWorkForce('Yes', 'Leadership and Work Force testing text');
    cy.clickMarkCompleteCheckbox();
    cy.SaveTaskSummary();
    Leadership.verifyTaskStatusIsCompleted();
    //  7.Members
     Members.clickMembersIfNotStarted();
    // Verify if the navigated to Members page
    cy.url().should('include', '/members');
    //Add a Member
    Members.addAnExistingMember('John Smith');
    Members.VerifyAddedMember('John Smith');
    Members.addANewMember('Alice Johnson', 'Past role description testing text');
    Members.VerifyAddedMember('Alice Johnson');
    Members.addANewMember('Bob Brown', 'Past role description for Bob Brown');
    Members.VerifyAddedMember('Bob Brown');
    Members.addANewMember('John Bora', 'Past role description for John Smith');  
    Members.VerifyAddedMember('John Bora');
    Members.addALeavingMember('Sarah White');
    Members.LeavingMemberVerification('Sarah White');
    Members.addALeavingMember('David Green');
    Members.LeavingMemberVerification('David Green');
    Members.addALeavingMember('Emma Black');
    Members.LeavingMemberVerification('Emma Black');
    // Click on the Mark Complete checkbox
    cy.clickMarkCompleteCheckbox();
    cy.SaveTaskSummary();
    Members.verifyTaskStatusIsCompleted();

   // 8. Trustees
    Trustees.clickTrusteesIfNotStarted();
    // Verify if the navigated to Trustees page
    cy.url().should('include', '/trustees-task');
    Trustees.addAnExistingTrustee('Michael Scott','Granting Officer', 'Yes');
    Trustees.verifyAddedTrustee('Michael Scott');
    Trustees.addANewTrustee('Pam Beesly', 'Past role description for Pam Beesly', 'Yes', 'Future role description for Pam Beesly');
    Trustees.verifyAddedTrustee('Pam Beesly');
    Trustees.addANewTrustee('Jim Halpert', 'Past role description for Jim Halpert', 'No', 'Future role description for Jim Halpert');
    Trustees.verifyAddedTrustee('Jim Halpert');
    Trustees.addALeavingTrustee('Dwight Schrute');
    Trustees.LeavingTrusteeVerification('Dwight Schrute');
    Trustees.addALeavingTrustee('Stanley Hudson');
    Trustees.LeavingTrusteeVerification('Stanley Hudson');
    Trustees.addALeavingTrustee('Phyllis Vance');
    Trustees.LeavingTrusteeVerification('Phyllis Vance');
    // Click on the Mark Complete checkbox
    cy.clickMarkCompleteCheckbox();
    cy.SaveTaskSummary();
    Trustees.verifyTaskStatusIsCompleted();

  //  //9.Governance Structure
    GovernanceStructure.clickGovernanceStructureIfNotStarted();
    // Verify if the navigated to Governance Structure page
    cy.url().should('include', '/governance-structure');
    //
    GovernanceStructure.ClickChangeGovernanceStructure('No', 'Governance Structure testing text');
    GovernanceStructure.ClickChangeProposedGovernanceStructure();
    cy.clickMarkCompleteCheckbox();
    cy.SaveTaskSummary();
    GovernanceStructure.verifyTaskStatusIsCompleted();

   // **About Transferring academies **/

   // 1. Details of Academies 
    Academies.clickDetailsOfAcademiesIfNotStarted();
    // Verify if the navigated to Details of Academies page
    cy.url().should('include', '/details-of-academies');
  //  Academies.AddAnAcademy('Cannon Lane Primary School', 'Yes', 'Yes');
    Academies.AddAnAcademy('St Marys C of E Primary and Nursery, Academy, Handsworth', 'No', 'No');
    // Click on the Mark Complete checkbox
    cy.clickMarkCompleteCheckbox();
    cy.SaveTaskSummary();


  //2. Reasons and Bemefits About Transferring Academies
    Logger.log("Selecting the Reasons and Benefits task");
    ReasonsAndBenefits.clickReasonsIfNotStarted();
    ReasonsAndBenefits.ClickChangeforStrategicNeeds('Strategic needs Testing text');
    ReasonsAndBenefits.ClickChangeforBenefits('Benefits Testing text');
    ReasonsAndBenefits.clickCompleteCheckbox();
   // Save and continue to complete the task
    cy.SaveTaskSummary();
     ReasonsAndBenefits.verifyTaskStatusIsCompleted();

   // 3. Risks
    Risks.clickRisksIfNotStarted();
    // Verify if the navigated to Risks page
    cy.url().should('include', '/risks');
    Risks.ClickChangeDueDiligence('Due Diligence testing text');
    Risks.ClickChangeRisksPupilNumber('Yes');
    Risks.ClickChangeTypeOfTransfer();
    Risks.ClickChangeOtherRisks('Other Risks testing text');
    Risks.ClickChangeFinancesPooled('GAGPooled');
    Risks.ClickChangeSurplusFunds('Surplus funds testing text');
    cy.wait(2000);
    cy.clickMarkCompleteCheckbox();
    cy.wait(2000);
    cy.SaveTaskSummary();
    Risks.verifyTaskStatusIsCompleted();


    // Outgoing Trust Details
    OutgoingTrust.clickDetailsofTrustIfNotStarted();
    // Verify if the navigated to Incoming Trust Details page
    cy.url().should('include', '/details-of-outgoing-trusts');
    OutgoingTrust.AddTrust('CANONIUM LEARNING TRUST');
    OutgoingTrust.EnterContactDetails('Michael Scott','Granting Officer', '07700 900 982','M.A@gov.uk');
    OutgoingTrust.SelectTrustClosure('Yes');
    OutgoingTrust.verifyOutgoingTrustIsAdded();
    cy.clickMarkCompleteCheckbox();
    cy.SaveTaskSummary();
    OutgoingTrust.verifyTaskStatusIsCompleted();

    // Declaration
    Declaration.clickDeclarationIfNotStarted();
    // Verify if the navigated to Declaration page
    cy.url().should('include', '/declaration-from-academy-trust-chair');
    //Enter Name of same joining Academy written in Academy section
    Declaration.ClickViewJoiningAcademyDeclaration('CANONIUM LEARNING TRUST','John Cena','11','11','2025');
    Declaration.ClickViewLeavingAcademyDeclaration('CANONIUM LEARNING TRUST','Michelle Loner','20','01','2026',);
    cy.clickMarkCompleteCheckbox();
    cy.SaveTaskSummary();
    Declaration.verifyTaskStatusIsCompleted();
    // Verify if all the tasks are completed
    Dashboardpage.verifyAllTasksAreCompleted();
   // Review the application
   cy.ReviewApplication();
   cy.SubmitApplication();
   //cy.getByClass('govuk-panel__title').should('have.text', 'Application submitted');
  //cy.getByClass('govuk-panel__body').contains('Your reference number'+ Cypress.env('referenceNumber')); 




 });
   

  
    
 
});
