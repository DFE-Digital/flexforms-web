import {EnvUrl} from "../Constants/cypressConstants";

export class GovUKPage{

    static selectors = {
        startButton: 'start-application-button',
        
    }

      static getHomePage() {
       cy.visit(Cypress.expose(EnvUrl));
    }
static scrollToStartButton() {
        cy.getById(this.selectors.startButton).scrollIntoView();
    }

    static clickStartBtn() {
        cy.getById(this.selectors.startButton).click();
        return this;
    }

    
}
export default GovUKPage;
