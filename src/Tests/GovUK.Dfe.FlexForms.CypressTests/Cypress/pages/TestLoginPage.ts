
export class TestLoginPage{

    static selectors = {
        email: 'Input.Email',
        continuebtn: 'test-login-button',
        nextbtn: 'button-next',
        signInbtn: 'button-sign-in',
        password:'password'
        
    }

    
 static enterUsername(username: string) {
        cy.getById(this.selectors.email).click().type(username);
        return this;
    
}
static clickNext() {
    cy.getById(this.selectors.nextbtn).contains('Next').click();
}

static clickContinue() {
    cy.getById(this.selectors.continuebtn).contains('Continue').click();

}


 static enterPassword(password: string) {
        cy.getById(this.selectors.email).click().type(password);
        return this;
    
}


static SignInBtn() {
    cy.getById(this.selectors.signInbtn).contains('Sign in').click();
    return this;


}

}
export default TestLoginPage;
