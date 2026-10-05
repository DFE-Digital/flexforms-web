using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

public sealed class ContributorManagementTests : PlaywrightTestBase
{
    private CreateApplicationResponse _application = null!;
    private readonly string _contributorEmail = TestEnvironment.RequireEnvironmentVariable("DEFAULT_USER_EMAIL");
    private readonly string _leadApplicantEmail = TestEnvironment.RequireEnvironmentVariable("ADMIN_EMAIL");

    [OneTimeSetUp]
    public async Task CreateApplicationWithContributorAsync()
    {
        var createRequest = ApplicationBuilder.CreateApplicationRequest(ApiConfig.TemplateId);
        _application = await ApplicationApi.CreateApplicationAsync(AdminApiClient, createRequest);
        await ApplicationApi.AddContributorToApplicationAsync(AdminApiClient, _application.ApplicationId,
            _contributorEmail);
    }

    [TestCase(TestName = "Admin can find who has access to an application by reference number")]
    [CiRetry]
    public async Task AdminCanFindWhoHasAccessToAnApplicationByReferenceNumberAsync()
    {
        var adminPage = new AdminPage(Page, Terminology);
        var contributorManagement = new ContributorManagement(Page, Terminology);

        await LoginAsync("admin");
        await Page.GotoAsync("/admin");
        await adminPage.OpenContributorManagementAsync();

        await contributorManagement.LookupAnApplicationByReferenceNumberAsync(_application.ApplicationReference);
        await contributorManagement.ExpectHasContributorAsync(_application.ApplicationReference, _contributorEmail);
    }

    [TestCase(TestName = "Admin can lookup application and invitees by user email address")]
    [CiRetry]
    public async Task AdminCanLookupApplicationAndInviteesByUserEmailAddressAsync()
    {
        var contributorManagement = new ContributorManagement(Page, Terminology);

        await LoginAsync("admin");
        await Page.GotoAsync("/admin/contributor-management");

        await contributorManagement.LookupApplicationsByUserAsync(_leadApplicantEmail);
        await contributorManagement.CreatedApplications
            .HasApplication(_application.ApplicationReference)
            .WithInvitee(_contributorEmail)
            .WithInviteLink()
            .VerifyAsync();
    }
}