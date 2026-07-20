// Import the necessary classes and methods
import GovUKPage from '../../pages/GovUKPage';
import TestLoginPage from '../../pages/TestLoginPage';
import Dashboardpage from '../../pages/Dashboardpage';
import { Logger } from '../../Common/logger';
import '../../support/commands';
import Contributors from '../../pages/ContributorsPage';
import ReasonsAndBenefits from '../../pages/TaskListPages/ReasonsAndBenefitsPageOutgoing'; 
import Academies from '../../pages/TaskListPages/DetailsofAcademies';
import {EnvUsername} from "../../Constants/cypressConstants";

describe('Create an Application', () => {

 beforeEach(() => {
        cy.login();
        cy.executeAccessibilityTests();
    });

  it('should navigate to the Gov.uk , scroll to the button and  click start button', () => {
    Logger.log("Scroll to the start button  on Gov.uk page"); 
    GovUKPage.scrollToStartButton();

   Logger.log("Clicking the start button on the dashboard");
     GovUKPage.clickStartBtn() ;
   // Wait for the page to load after clicking the start button

    // Add assertions or further actions here as needed
    Logger.log("Verify if redirected to TestLogin page"); 
    cy.url().should('include', '/TestLogin'); // Example assertion

    Logger.log("Logging in with the test user");
    TestLoginPage.enterUsername(Cypress.expose(EnvUsername));
     Logger.log("Clicking the Continue button on the TestLogin page");
    TestLoginPage.SignInBtn();
    // Add assertions or further actions here as needed

    //  assertion to check if redirected to dashboard
    Logger.log("Verify if redirected to Dashboard page");
    cy.url().should('include', '/dashboard'); 
    //Go to Dashboard and click Start New Application
    
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
    Contributors.verifyContributor();
    Logger.log("Clicking the Proceed button on Contributors page");
    Contributors.ClickProceedBtn();

    // Verify if the navigated to Task List Page
    Logger.log("Verify if redirected to Task List page");
    cy.url().should('include', '/applications/');
    // Click on the Invite Contributors button
   Contributors.inviteContributors();

    // Select Details of Academies task
       Logger.log("Clicking the Details of Academies task");
       Academies.clickDetailsOfAcademies();
       // Verify if the navigated to Details of Academies page
       cy.url().should('include', '/summary');
       // Click on the Change Academies button
       Academies.clickChangeAcademies();
       // Search for the academy
       Logger.log("Searching for the academy");
       Academies.searchAcademy();
       // Verify if the academy is selected
       Logger.log("Verifying if the academy is selected");
       Academies.verifyselectedAcademy();
       // Click on the Mark Complete checkbox
       Logger.log("Clicking the Mark Complete checkbox");
       Academies.clickMarkCompleteCheckbox();
       // Click on the Save and Continue button
       Logger.log("Clicking the Save and Continue button on Details of Academies page");
       Academies.clickSaveAndContinue();
 
  });
 
  });

