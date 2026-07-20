export class Dashboardpage{

    static selectors = {
        startNewApplicationBtn: 'start-new-application-button',
        referenceNumber: 'application-reference-number',
        
    }

    static clickStartBtn() {
        cy.getById(this.selectors.startNewApplicationBtn).click();
        return this;
    }

     static extractReferenceNumber() {
        // Extracting the application number from the URL
         cy.url().then((url) => {
            // Split the URL by '/' and extract the desired segment
            const segments = url.split('/');
            const appRefNum = segments[segments.length - 1]; // Extracts the application reference number

            // Assert the extracted value
            expect(appRefNum).to.match(/^TRF-\d{8}-\d{3}$/); // Should match the pattern TRF-123-456

            // Log the value (optional)
            cy.log(`Extracted Application Reference Number from URL: ${appRefNum}`);
            // Store the value as an environment variable for later use;
            Cypress.expose('applicationReferenceNumber', appRefNum);
        });
    }

    static verifyAllTasksAreCompleted(){

    }


    
}

export default Dashboardpage;
