import {EnvAuthKey, EnvCypressSecret, EnvUrl, EnvUsername} from "../Constants/cypressConstants";


export class AuthenticationInterceptor {
    register() {
        cy.env([EnvAuthKey, EnvCypressSecret]).then(({ authKey, cypress_secret }) => {
            cy.intercept(
                {
                    url: Cypress.expose(EnvUrl) + "/**",
                    middleware: true,
                },
                (req) => {
                    // Set an auth header on every request made by the browser
                    req.headers = {
                        ...req.headers,
                        Authorization: `Bearer ${authKey}`,
                        "x-user-context-name": (Cypress.expose(EnvUsername)), // must be present, but not used
                        "x-user-context-id": "", // must be present for antiforgery claims
                        "x-user-ad-id": "",
                        "x-service-email": Cypress.expose(EnvUsername),
                        "x-cypress-test": "true",
                        "x-service-api-key": cypress_secret,
                        "X-Tenant-ID":"11111111-1111-4111-8111-111111111111"

                    };
                },
            ).as("AuthInterceptor");
        });
    }
}