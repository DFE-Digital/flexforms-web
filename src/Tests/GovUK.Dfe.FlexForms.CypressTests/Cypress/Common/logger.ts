
export class Logger {
    public static log(message: string) {
        cy.log("log", message);
    }
}
