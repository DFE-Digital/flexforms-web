// ***********************************************************
// This example support/index.js is processed and
// loaded automatically before your test files.
//
// This is a great place to put global configuration and
// behavior that modifies Cypress.
//
// You can change the location of this file or turn off
// automatically serving support files with the
// 'supportFile' configuration option.
//
// You can read more here:
// https://on.cypress.io/configuration
// ***********************************************************

// Import commands.js using ES2015 syntax:

import "./commands";
import { RuleObject } from "axe-core";
import { AuthenticationInterceptor } from "../auth/authenticationInterceptor";

// Block ASP.NET Core development tools that cause issues with Cypress proxy
Cypress.on('uncaught:exception', (err) => {
    // Ignore SignalR and browser refresh errors that cause "Invalid status code: 0"
    if (err.message.includes('SignalR') || 
        err.message.includes('browser-refresh') ||
        err.message.includes('browserLink')) {
        return false; // Don't fail the test
    }
    return true;
});

beforeEach(() => {
    // Block problematic ASP.NET Core development middleware requests
    // These cause "Invalid status code: 0" errors when running against localhost
    cy.intercept('/_framework/aspnetcore-browser-refresh.js*', { 
        statusCode: 204,
        body: ''
    }).as('blockBrowserRefresh');
    
    cy.intercept('/_vs/browserLink*', { 
        statusCode: 204,
        body: ''
    }).as('blockBrowserLink');
    
    cy.intercept('/_framework/**', { 
        statusCode: 204,
        body: ''
    }).as('blockFramework');

    new AuthenticationInterceptor().register();
});



declare global {
    namespace Cypress {
        interface Chainable {
            getByTestId(id: string): Chainable<Element>;
            containsByTestId(id: string): Chainable<Element>;
            getById(id: string): Chainable<Element>;
            getByClass(className: string): Chainable<Element>;
            getByName(name: string): Chainable<Element>;
            getByRole(role: string): Chainable<Element>;
            getByLabelFor(labelFor: string): Chainable<Element>;
            getByRadioOption(radioText: string): Chainable<Element>;
            login(): Chainable<Element>;
            SaveAndContinue(): Chainable<Element>;
            SaveTaskSummary(): Chainable<Element>;
            clickMarkCompleteCheckbox(): Chainable<Element>;
            ClickContinue(): Chainable<Element>;
            ReviewApplication():Chainable<Element>;
            SubmitApplication():Chainable<Element>;
            loginWithCredentials(): Chainable<Element>;
            assertChildList(selector: string, values: string[]): Chainable<Element>;
            executeAccessibilityTests(ruleExclusions?: RuleObject): Chainable<Element>;
            enterDate(idPrefix: string, day: string, month: string, year: string): Chainable<Element>;
            checkDate(idPrefix: string, day: string, month: string, year: string): Chainable<Element>;
            hasAddress(id: string, line1: string, line2: string, line3: string): Chainable<Element>;
            typeFast(text: string): Chainable<Element>;
            attachFixtureFile(selector: string, fixtureRelativePath: string, fileName?: string, mimeType?: string): Chainable<Element>;
        }
    }
}
